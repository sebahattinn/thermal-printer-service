using Microsoft.Extensions.DependencyInjection;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// Mode -&gt; uygun IPrinterService singleton'unu döner. Controller bu fabrika ile
/// concrete tipleri tanımaz; yeni bir bağlantı tipi eklemek burada tek satır.
/// </summary>
public sealed class PrinterFactory : IPrinterFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PrinterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPrinterService Create(ConnectionMode mode) => mode switch
    {
        ConnectionMode.Usb => _serviceProvider.GetRequiredService<UsbPrinter>(),
        ConnectionMode.Lan => _serviceProvider.GetRequiredService<LanPrinter>(),
        ConnectionMode.Mock => _serviceProvider.GetRequiredService<MockPrinter>(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Bilinmeyen bağlantı modu.")
    };
}
