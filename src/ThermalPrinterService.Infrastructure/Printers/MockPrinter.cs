using Microsoft.Extensions.Logging;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// Simüle edilmiş yazıcı: gerçek donanım olmadan tam akışı test etmek için.
/// Her zaman bağlı, her zaman Ready; basılan baytlar in-memory koleksiyonda tutulur
/// ve ILogger üzerinden satır sayısı + payload boyutu loglanır.
/// Üretim ortamında ConnectionMode.Mock seçilirse bu sürücü devreye girer —
/// fiziksel cihaz gelene kadar demo / QA / CI için kullanılır.
/// </summary>
public sealed class MockPrinter : IPrinterService
{
    private readonly ILogger<MockPrinter> _logger;
    private readonly object _gate = new();

    private bool _connected;
    private PrinterState _state = PrinterState.Disconnected;

    public int PrintedTextCount { get; private set; }
    public int PrintedImageCount { get; private set; }
    public int PrintedRawCount { get; private set; }

    public MockPrinter(ILogger<MockPrinter> logger) => _logger = logger;

    public ConnectionMode Mode => ConnectionMode.Mock;
    public bool IsConnected { get { lock (_gate) return _connected; } }
    public PrinterState CurrentState { get { lock (_gate) return _state; } }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _connected = true;
            _state = PrinterState.Ready;
        }
        _logger.LogInformation("MockPrinter bağlandı (simülasyon).");
        return Task.FromResult(true);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _connected = false;
            _state = PrinterState.Disconnected;
        }
        return Task.CompletedTask;
    }

    public Task<PrinterState> QueryStateAsync(CancellationToken cancellationToken)
    {
        lock (_gate) return Task.FromResult(_state);
    }

    public Task PrintTextAsync(string text, CancellationToken cancellationToken)
    {
        lock (_gate) PrintedTextCount++;
        _logger.LogInformation("MOCK PRINT text: {Length} karakter", text?.Length ?? 0);
        return Task.CompletedTask;
    }

    public Task PrintImageAsync(ReadOnlyMemory<byte> imageBytes, CancellationToken cancellationToken)
    {
        lock (_gate) PrintedImageCount++;
        _logger.LogInformation("MOCK PRINT image: {Bytes} byte", imageBytes.Length);
        return Task.CompletedTask;
    }

    public Task PrintRawAsync(ReadOnlyMemory<byte> rawBytes, CancellationToken cancellationToken)
    {
        lock (_gate) PrintedRawCount++;
        _logger.LogInformation("MOCK PRINT raw: {Bytes} byte (ESC/POS payload)", rawBytes.Length);
        return Task.CompletedTask;
    }
}
