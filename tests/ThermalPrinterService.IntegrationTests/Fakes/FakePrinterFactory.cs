using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.IntegrationTests.Fakes;

/// <summary>
/// Hangi mode istenirse istensin tek bir FakePrinter dondurur; testler bu
/// nesnenin durumunu fixture uzerinden okur/yazar.
/// </summary>
public sealed class FakePrinterFactory : IPrinterFactory
{
    private readonly FakePrinter _printer;

    public FakePrinterFactory(FakePrinter printer)
    {
        _printer = printer;
    }

    public IPrinterService Create(ConnectionMode mode)
    {
        _printer.Mode = mode;
        return _printer;
    }
}
