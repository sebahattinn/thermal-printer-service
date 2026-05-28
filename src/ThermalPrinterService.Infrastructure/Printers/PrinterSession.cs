using ThermalPrinterService.Application.Abstractions;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// Aktif sürücüyü tutan singleton oturum. Thread-safe set/clear sağlar;
/// PrintWorker ve ReconnectBackgroundService bunu okur.
/// </summary>
public sealed class PrinterSession : IPrinterSession
{
    private readonly object _gate = new();
    private IPrinterService? _current;

    public IPrinterService? Current
    {
        get { lock (_gate) return _current; }
    }

    public void Set(IPrinterService printer)
    {
        ArgumentNullException.ThrowIfNull(printer);
        lock (_gate) _current = printer;
    }

    public void Clear()
    {
        lock (_gate) _current = null;
    }
}
