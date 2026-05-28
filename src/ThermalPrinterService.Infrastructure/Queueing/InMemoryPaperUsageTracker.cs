using Microsoft.Extensions.Options;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Application.DTOs;
using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Infrastructure.Configuration;
using ThermalPrinterService.Infrastructure.Printers;

namespace ThermalPrinterService.Infrastructure.Queueing;

/// <summary>
/// Rulo kullanımını bellekte tutar. Servis restart edilirse sıfırlanır.
/// </summary>
public sealed class InMemoryPaperUsageTracker : IPaperUsageTracker
{
    private readonly IOptionsMonitor<PrinterOptions> _options;
    private readonly object _gate = new();

    private double _usedMm;
    private int _jobCount;

    public InMemoryPaperUsageTracker(IOptionsMonitor<PrinterOptions> options)
    {
        _options = options;
    }

    public void RegisterPrinted(PrintJob job)
    {
        var paperOpts = _options.CurrentValue.Paper;
        var mm = PaperUsageEstimator.EstimateMm(job, paperOpts);
        lock (_gate)
        {
            _usedMm += mm;
            _jobCount++;
        }
    }

    public void ResetRoll()
    {
        lock (_gate)
        {
            _usedMm = 0;
            _jobCount = 0;
        }
    }

    public PaperInfo GetInfo()
    {
        var paperOpts = _options.CurrentValue.Paper;
        var rollMm = paperOpts.RollLengthMeters * 1000.0;

        double used, jobs;
        lock (_gate) { used = _usedMm; jobs = _jobCount; }

        var remaining = Math.Max(0.0, rollMm - used);
        var pct = rollMm > 0 ? Math.Clamp(100.0 * remaining / rollMm, 0, 100) : 0;
        double? avg = jobs > 0 ? used / jobs : null;
        int? jobsRemaining = avg is > 0 ? (int)(remaining / avg.Value) : null;

        return new PaperInfo
        {
            UsedMm = Math.Round(used, 1),
            RollLengthMm = rollMm,
            RemainingMm = Math.Round(remaining, 1),
            RemainingPercent = Math.Round(pct, 2),
            PrintedJobCount = (int)jobs,
            AverageJobMm = avg is null ? null : Math.Round(avg.Value, 2),
            EstimatedJobsRemaining = jobsRemaining
        };
    }
}
