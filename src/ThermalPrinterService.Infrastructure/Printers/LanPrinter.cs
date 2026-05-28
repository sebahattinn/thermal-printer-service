using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Configuration;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// TCP üzerinden (yaygın olarak 9100) ESC/POS termal yazıcıya basar.
/// Tek bağlantı tek sürücüye aittir; tüm okuma/yazmalar bir semafor arkasında serileştirilir.
/// </summary>
public sealed class LanPrinter : IPrinterService, IDisposable
{
    private readonly IOptionsMonitor<PrinterOptions> _options;
    private readonly ILogger<LanPrinter> _logger;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private PrinterState _state = PrinterState.Disconnected;

    public LanPrinter(IOptionsMonitor<PrinterOptions> options, ILogger<LanPrinter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public ConnectionMode Mode => ConnectionMode.Lan;
    public bool IsConnected => _tcp?.Connected ?? false;
    public PrinterState CurrentState => _state;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            var lan = _options.CurrentValue.Lan;
            if (string.IsNullOrWhiteSpace(lan.Host))
            {
                _logger.LogWarning("LAN Host boş; bağlantı kurulamadı.");
                _state = PrinterState.CommError;
                return false;
            }

            SafeClose();

            _tcp = new TcpClient
            {
                ReceiveTimeout = lan.ReadTimeoutMs,
                SendTimeout = lan.WriteTimeoutMs,
                NoDelay = true
            };

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(lan.ConnectTimeoutMs);

            await _tcp.ConnectAsync(lan.Host, lan.Port, connectCts.Token);
            _stream = _tcp.GetStream();

            var init = EscPosCommands.Init.ToArray();
            await _stream.WriteAsync(init, cancellationToken);

            _state = PrinterState.Ready;
            _logger.LogInformation("LAN bağlandı: {Host}:{Port}", lan.Host, lan.Port);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _state = PrinterState.CommError;
            _logger.LogError("LAN bağlantı timeout.");
            SafeClose();
            return false;
        }
        catch (Exception ex)
        {
            _state = PrinterState.CommError;
            _logger.LogError(ex, "LAN bağlantı hatası.");
            SafeClose();
            return false;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            SafeClose();
            _state = PrinterState.Disconnected;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<PrinterState> QueryStateAsync(CancellationToken cancellationToken)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            if (_stream is null || _tcp is null || !_tcp.Connected)
            {
                _state = PrinterState.Disconnected;
                return _state;
            }

            var readTimeoutMs = _options.CurrentValue.Lan.ReadTimeoutMs;

            // KP-300/302 manuel: 4 ayrı DLE EOT sorgusu birlikte tam durum verir.
            // Tek bir sorgu (DLE EOT 1) yalnızca online/offline der; sebebini söylemez.
            var s1 = await QueryOneAsync(EscPosCommands.StatusRequestPrinter.ToArray(), readTimeoutMs, cancellationToken);
            var s2 = await QueryOneAsync(EscPosCommands.StatusRequestOffline.ToArray(), readTimeoutMs, cancellationToken);
            var s3 = await QueryOneAsync(EscPosCommands.StatusRequestError.ToArray(), readTimeoutMs, cancellationToken);
            var s4 = await QueryOneAsync(EscPosCommands.StatusRequestPaperSensor.ToArray(), readTimeoutMs, cancellationToken);

            _state = EscPosCommands.CombineStates(
                EscPosCommands.ParsePrinterStatus(s1),
                EscPosCommands.ParseOfflineStatus(s2),
                EscPosCommands.ParseErrorStatus(s3),
                EscPosCommands.ParsePaperSensorStatus(s4));

            return _state;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _state = PrinterState.CommError;
            return _state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LAN status sorgusu başarısız.");
            _state = PrinterState.CommError;
            return _state;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<byte> QueryOneAsync(byte[] request, int readTimeoutMs, CancellationToken cancellationToken)
    {
        await _stream!.WriteAsync(request, cancellationToken);

        var buffer = new byte[1];
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readCts.CancelAfter(readTimeoutMs);

        var read = await _stream.ReadAsync(buffer, readCts.Token);
        if (read == 0) throw new IOException("Status yanıtı 0 byte (peer kapattı).");
        return buffer[0];
    }

    public async Task PrintTextAsync(string text, CancellationToken cancellationToken)
    {
        var payload = EscPosCommands.BuildText(text);
        await WriteAsync(payload, cancellationToken);
    }

    public async Task PrintImageAsync(ReadOnlyMemory<byte> imageBytes, CancellationToken cancellationToken)
    {
        var payload = EscPosCommands.WrapImagePayload(imageBytes.Span);
        await WriteAsync(payload, cancellationToken);
    }

    public Task PrintRawAsync(ReadOnlyMemory<byte> rawBytes, CancellationToken cancellationToken)
        => WriteAsync(rawBytes.ToArray(), cancellationToken);

    private async Task WriteAsync(byte[] payload, CancellationToken cancellationToken)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            if (_stream is null || _tcp is null || !_tcp.Connected)
                throw new InvalidOperationException("LAN yazıcı bağlı değil.");

            await _stream.WriteAsync(payload, cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private void SafeClose()
    {
        try { _stream?.Dispose(); } catch { /* yutuluyor */ }
        try { _tcp?.Close(); } catch { /* yutuluyor */ }
        _stream = null;
        _tcp = null;
    }

    public void Dispose()
    {
        SafeClose();
        _ioLock.Dispose();
    }
}
