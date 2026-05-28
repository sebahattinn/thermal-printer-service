using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Configuration;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// USB termal yazıcı sürücüsü. Çoğu modern termal yazıcı, sürücü kurulduğunda
/// virtual COM port olarak görünür. Burada System.IO.Ports.SerialPort kullanıyoruz;
/// alternatif (libusb vb.) bir transport gerekirse sadece bu sınıfı değiştirmek yeter.
/// </summary>
public sealed class UsbPrinter : IPrinterService, IDisposable
{
    private readonly IOptionsMonitor<PrinterOptions> _options;
    private readonly ILogger<UsbPrinter> _logger;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    private SerialPort? _port;
    private PrinterState _state = PrinterState.Disconnected;

    public UsbPrinter(IOptionsMonitor<PrinterOptions> options, ILogger<UsbPrinter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public ConnectionMode Mode => ConnectionMode.Usb;
    public bool IsConnected => _port?.IsOpen ?? false;
    public PrinterState CurrentState => _state;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            var usb = _options.CurrentValue.Usb;
            if (string.IsNullOrWhiteSpace(usb.PortName))
            {
                _logger.LogWarning("USB PortName boş; bağlantı kurulamadı.");
                _state = PrinterState.CommError;
                return false;
            }

            SafeClose();

            _port = new SerialPort(usb.PortName, usb.BaudRate, usb.Parity, usb.DataBits, usb.StopBits)
            {
                Handshake = usb.Handshake,
                ReadTimeout = usb.ReadTimeoutMs,
                WriteTimeout = usb.WriteTimeoutMs,
                Encoding = System.Text.Encoding.UTF8
            };

            _port.Open();

            // ESC @ ile yazıcıyı reset/init et.
            _port.Write(EscPosCommands.Init.ToArray(), 0, EscPosCommands.Init.Length);

            _state = PrinterState.Ready;
            _logger.LogInformation("USB bağlandı: {Port} @ {Baud}", usb.PortName, usb.BaudRate);
            return true;
        }
        catch (Exception ex)
        {
            _state = PrinterState.CommError;
            _logger.LogError(ex, "USB bağlantı hatası.");
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
            if (_port is null || !_port.IsOpen)
            {
                _state = PrinterState.Disconnected;
                return _state;
            }

            // 4 ayrı DLE EOT sorgusu; KP-300/302 manuel sayfa 67-69.
            var s1 = QueryOne(EscPosCommands.StatusRequestPrinter);
            var s2 = QueryOne(EscPosCommands.StatusRequestOffline);
            var s3 = QueryOne(EscPosCommands.StatusRequestError);
            var s4 = QueryOne(EscPosCommands.StatusRequestPaperSensor);

            _state = EscPosCommands.CombineStates(
                EscPosCommands.ParsePrinterStatus(s1),
                EscPosCommands.ParseOfflineStatus(s2),
                EscPosCommands.ParseErrorStatus(s3),
                EscPosCommands.ParsePaperSensorStatus(s4));

            return _state;
        }
        catch (TimeoutException)
        {
            _state = PrinterState.CommError;
            return _state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "USB status sorgusu başarısız.");
            _state = PrinterState.CommError;
            return _state;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private byte QueryOne(ReadOnlySpan<byte> request)
    {
        var req = request.ToArray();
        _port!.Write(req, 0, req.Length);
        return (byte)_port.ReadByte();
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
            if (_port is null || !_port.IsOpen)
                throw new InvalidOperationException("USB yazıcı bağlı değil.");

            _port.Write(payload, 0, payload.Length);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private void SafeClose()
    {
        try
        {
            if (_port is { IsOpen: true }) _port.Close();
            _port?.Dispose();
        }
        catch
        {
            // Kapatmada oluşan hata önemli değil; loglamaya gerek yok.
        }
        finally
        {
            _port = null;
        }
    }

    public void Dispose()
    {
        SafeClose();
        _ioLock.Dispose();
    }
}
