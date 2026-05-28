using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Application.StateMachine;
using ThermalPrinterService.Infrastructure.Configuration;
using ThermalPrinterService.Infrastructure.Connection;
using ThermalPrinterService.Infrastructure.Logging;
using ThermalPrinterService.Infrastructure.Printers;
using ThermalPrinterService.Infrastructure.Queueing;

namespace ThermalPrinterService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPrinterServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PrinterOptions>()
            .Bind(configuration.GetSection(PrinterOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IPrinterStateMachine, PrinterStateMachine>();

        services.AddSingleton<UsbPrinter>();
        services.AddSingleton<LanPrinter>();
        services.AddSingleton<MockPrinter>();

        services.AddSingleton<IPrinterFactory, PrinterFactory>();
        services.AddSingleton<IPrinterSession, PrinterSession>();

        services.AddSingleton<IImageEncoder, MonochromeImageEncoder>();
        services.AddSingleton<IReceiptComposer, EscPosReceiptComposer>();

        services.AddSingleton<IPrintQueue, InMemoryPrintQueue>();
        services.AddSingleton<ILogStore, JsonFileLogStore>();
        services.AddSingleton<IFailedJobArchive, FileSystemFailedJobArchive>();

        services.AddSingleton<IIdempotencyStore>(_ => new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10)));
        services.AddSingleton<IPaperUsageTracker, InMemoryPaperUsageTracker>();

        services.AddHostedService<FailedJobRehydrationService>();
        services.AddHostedService<PrintWorker>();
        services.AddHostedService<ReconnectBackgroundService>();

        return services;
    }
}
