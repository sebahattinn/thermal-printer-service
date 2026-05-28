namespace ThermalPrinterService.Domain.Enums;

public enum PrinterState
{
    Disconnected = 0,
    Ready = 1,
    PaperOut = 2,
    PaperJam = 3,
    CoverOpen = 4,
    Overheat = 5,
    CommError = 6,
    UnknownCommand = 7
}
