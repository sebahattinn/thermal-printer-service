using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.IntegrationTests.Fakes;

/// <summary>
/// Test icin tam kontrol edilebilir IPrinterService.
/// </summary>
public sealed class FakePrinter : IPrinterService
{
    private readonly object _gate = new();
    private bool _connected;
    private PrinterState _state = PrinterState.Disconnected;

    public ConnectionMode Mode { get; set; } = ConnectionMode.Lan;
    public bool IsConnected { get { lock (_gate) return _connected; } }
    public PrinterState CurrentState { get { lock (_gate) return _state; } }

    public bool FailConnect { get; set; }
    public bool FailNextPrint { get; set; }
    public PrinterState QueriedState { get; set; } = PrinterState.Ready;

    public List<string> PrintedTexts { get; } = new();
    public List<byte[]> PrintedImages { get; } = new();
    public List<byte[]> PrintedRaw { get; } = new();
    public int ConnectCallCount { get; private set; }

    public void Reset()
    {
        lock (_gate)
        {
            _connected = false;
            _state = PrinterState.Disconnected;
            FailConnect = false;
            FailNextPrint = false;
            QueriedState = PrinterState.Ready;
            PrintedTexts.Clear();
            PrintedImages.Clear();
            PrintedRaw.Clear();
            ConnectCallCount = 0;
        }
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ConnectCallCount++;
            if (FailConnect)
            {
                _connected = false;
                _state = PrinterState.CommError;
                return Task.FromResult(false);
            }
            _connected = true;
            _state = PrinterState.Ready;
            return Task.FromResult(true);
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _connected = false;
            _state = PrinterState.Disconnected;
            return Task.CompletedTask;
        }
    }

    public Task<PrinterState> QueryStateAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _state = QueriedState;
            return Task.FromResult(_state);
        }
    }

    public Task PrintTextAsync(string text, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (FailNextPrint) throw new InvalidOperationException("simulated text failure");
            PrintedTexts.Add(text);
            return Task.CompletedTask;
        }
    }

    public Task PrintImageAsync(ReadOnlyMemory<byte> imageBytes, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (FailNextPrint) throw new InvalidOperationException("simulated image failure");
            PrintedImages.Add(imageBytes.ToArray());
            return Task.CompletedTask;
        }
    }

    public Task PrintRawAsync(ReadOnlyMemory<byte> rawBytes, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (FailNextPrint) throw new InvalidOperationException("simulated raw failure");
            PrintedRaw.Add(rawBytes.ToArray());
            return Task.CompletedTask;
        }
    }

    public void ResetCounters()
    {
        lock (_gate)
        {
            PrintedTexts.Clear();
            PrintedImages.Clear();
            PrintedRaw.Clear();
            ConnectCallCount = 0;
        }
    }
}
