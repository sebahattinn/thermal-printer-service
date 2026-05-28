namespace ThermalPrinterService.Domain.Enums;

public enum ConnectionMode
{
    Usb = 0,
    Lan = 1,
    Mock = 2  // Donanım bağımsız simülasyon; demo + geliştirme + uçtan uca test için
}
