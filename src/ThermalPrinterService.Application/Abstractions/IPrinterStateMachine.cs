using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Application.Abstractions;

/// <summary>
/// Yazıcı durumunu yöneten state machine sözleşmesi.
/// Geçişler önceden tanımlanmış kurallarla doğrulanır;
/// geçersiz geçişler InvalidOperationException fırlatır.
/// </summary>
public interface IPrinterStateMachine
{
    PrinterState Current { get; }
    event Action<PrinterState, PrinterState>? Transitioned;

    bool CanTransitionTo(PrinterState next);
    void TransitionTo(PrinterState next);
    void Reset();
}
