using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThermalPrinterService.Application.Abstractions;

namespace ThermalPrinterService.Infrastructure.Queueing;

/// <summary>
/// Servis başlangıcında failed-jobs/ klasörünü tarar ve restart'tan önce
/// kalmış failed job'ları queue dict'e geri yükler — /reprint için bulunabilir olurlar.
/// Job'lar yeniden kuyruğa enqueue EDİLMEZ; sadece dict'te tutulur. Operatör/UI
/// "Tekrar Bastır" deyince kuyruğa girer.
/// </summary>
public sealed class FailedJobRehydrationService : IHostedService
{
    private readonly IFailedJobArchive _archive;
    private readonly IPrintQueue _queue;
    private readonly ILogger<FailedJobRehydrationService> _logger;

    public FailedJobRehydrationService(
        IFailedJobArchive archive,
        IPrintQueue queue,
        ILogger<FailedJobRehydrationService> logger)
    {
        _archive = archive;
        _queue = queue;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var jobs = await _archive.LoadAllAsync(cancellationToken);
            if (jobs.Count == 0)
            {
                _logger.LogInformation("Disk'te bekleyen failed job yok.");
                return;
            }

            foreach (var job in jobs) _queue.Rehydrate(job);
            _logger.LogInformation("Disk'ten {Count} failed job yeniden yüklendi.", jobs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed job rehydration başarısız oldu.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
