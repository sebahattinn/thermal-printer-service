using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Application.StateMachine;

/// <summary>
/// Yazıcı state machine geçiş matrisinin tek kaynaklı tanımı.
/// İzin verilen geçişler dışındaki tüm geçişler InvalidOperationException atar.
/// </summary>
public static class PrinterTransition
{
    private static readonly IReadOnlyDictionary<PrinterState, IReadOnlySet<PrinterState>> _allowed
        = BuildMatrix();

    public static IReadOnlySet<PrinterState> AllowedFrom(PrinterState state) => _allowed[state];

    public static bool IsAllowed(PrinterState from, PrinterState to)
        => from == to || _allowed[from].Contains(to);

    private static IReadOnlyDictionary<PrinterState, IReadOnlySet<PrinterState>> BuildMatrix()
    {
        // Tüm hata durumları (Ready dışındaki çalışma hataları).
        var faults = new HashSet<PrinterState>
        {
            PrinterState.PaperOut,
            PrinterState.PaperJam,
            PrinterState.CoverOpen,
            PrinterState.Overheat,
            PrinterState.UnknownCommand
        };

        var map = new Dictionary<PrinterState, IReadOnlySet<PrinterState>>();

        // Disconnected: bağlantı kuruldu ya da daha kurulurken iletişim hatası alındı.
        map[PrinterState.Disconnected] = new HashSet<PrinterState>
        {
            PrinterState.Ready,
            PrinterState.CommError
        };

        // Ready: her şeye geçebilir; hatalar veya disconnect.
        map[PrinterState.Ready] = new HashSet<PrinterState>(faults)
        {
            PrinterState.CommError,
            PrinterState.Disconnected
        };

        // Tüm fault durumları: Ready'ye iyileşir, Disconnected'a düşer ya da CommError'a kayabilir.
        foreach (var f in faults)
        {
            map[f] = new HashSet<PrinterState>
            {
                PrinterState.Ready,
                PrinterState.Disconnected,
                PrinterState.CommError
            };
        }

        // CommError: ya bağlantıyı tamamen kaybeder ya da yeniden sorguyla
        // bilinen herhangi bir duruma toparlanır.
        map[PrinterState.CommError] = new HashSet<PrinterState>
        {
            PrinterState.Disconnected,
            PrinterState.Ready,
            PrinterState.PaperOut,
            PrinterState.PaperJam,
            PrinterState.CoverOpen,
            PrinterState.Overheat,
            PrinterState.UnknownCommand
        };

        return map;
    }
}
