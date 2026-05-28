using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.IntegrationTests.Fakes;

namespace ThermalPrinterService.IntegrationTests;

/// <summary>
/// Tum integration testlerinin paylastigi WebApplicationFactory.
/// - IPrinterFactory yerine FakePrinterFactory enjekte eder.
/// - Loglari benzersiz bir gecici dosyaya yonlendirir.
/// - Backoff / polling sureleri kisaltilir, boylece testler hizli kosar.
/// </summary>
public sealed class PrinterApiFixture : WebApplicationFactory<Program>
{
    public FakePrinter FakePrinter { get; } = new();
    public string LogFilePath { get; }
    public string FailedJobsDir { get; }

    public PrinterApiFixture()
    {
        LogFilePath = Path.Combine(Path.GetTempPath(),
            $"printer-it-logs-{Guid.NewGuid():N}.json");
        FailedJobsDir = Path.Combine(Path.GetTempPath(),
            $"printer-it-failed-{Guid.NewGuid():N}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Printer:Logs:FilePath"] = LogFilePath,
                ["Printer:Logs:FailedJobsDirectory"] = FailedJobsDir,
                ["Printer:StatusPollIntervalMs"] = "80",
                ["Printer:Reconnect:InitialDelayMs"] = "30",
                ["Printer:Reconnect:MaxDelayMs"] = "100",
                ["Printer:Reconnect:Multiplier"] = "2.0",
                ["Printer:Reconnect:JitterFactor"] = "0.0"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Gercek factory yerine fake; FakePrinter singleton olarak da goruluyor
            // ki testler ayni nesneyi DI'dan veya fixture'dan elde edebilsin.
            services.RemoveAll<IPrinterFactory>();
            services.AddSingleton(FakePrinter);
            services.AddSingleton<IPrinterFactory, FakePrinterFactory>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        try { if (File.Exists(LogFilePath)) File.Delete(LogFilePath); } catch { /* gozardi */ }
        try { if (Directory.Exists(FailedJobsDir)) Directory.Delete(FailedJobsDir, recursive: true); } catch { /* gozardi */ }
    }
}
