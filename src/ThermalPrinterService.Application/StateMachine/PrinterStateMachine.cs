using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Application.StateMachine;

/// <summary>
/// Thread-safe in-memory state machine. Singleton olarak DI'a kaydedilmelidir.
/// </summary>
public sealed class PrinterStateMachine : IPrinterStateMachine
{
    private readonly object _gate = new();
    private PrinterState _current = PrinterState.Disconnected;

    public PrinterState Current
    {
        get { lock (_gate) return _current; }
    }

    public event Action<PrinterState, PrinterState>? Transitioned;

    public bool CanTransitionTo(PrinterState next)
    {
        lock (_gate) return PrinterTransition.IsAllowed(_current, next);
    }

    public void TransitionTo(PrinterState next)
    {
        PrinterState previous;
        bool changed;
        lock (_gate)
        {
            if (!PrinterTransition.IsAllowed(_current, next))
                throw new InvalidOperationException($"Geçersiz state geçişi: {_current} -> {next}");

            previous = _current;
            changed = previous != next;
            _current = next;
        }
        if (changed) Transitioned?.Invoke(previous, next);
    }

    public void Reset()
    {
        PrinterState previous;
        bool changed;
        lock (_gate)
        {
            previous = _current;
            changed = previous != PrinterState.Disconnected;
            _current = PrinterState.Disconnected;
        }
        if (changed) Transitioned?.Invoke(previous, PrinterState.Disconnected);
    }
}
