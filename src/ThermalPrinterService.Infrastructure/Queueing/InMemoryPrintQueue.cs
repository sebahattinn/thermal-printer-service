using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Application.DTOs;
using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Infrastructure.Queueing;

/// <summary>
/// System.Threading.Channels üzerine kurulu, asenkron FIFO kuyruk. Job'lar ayrı
/// bir ConcurrentDictionary'de tutulur; /reprint ile geçmişe dönüş ve /status için
/// sayım yapılabilir.
/// </summary>
public sealed class InMemoryPrintQueue : IPrintQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly ConcurrentDictionary<Guid, PrintJob> _jobs = new();

    // Son tamamlanan job (status response'da gosterilir).
    private PrintJob? _lastCompleted;
    private readonly object _lastCompletedGate = new();

    // ETA için: son N başarılı işin sürelerinin yuvarlanan ortalaması.
    private const int EtaWindowSize = 20;
    private readonly Queue<double> _recentDurationsMs = new(EtaWindowSize);
    private readonly object _etaGate = new();

    public Task<Guid> EnqueueAsync(PrintJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        _jobs[job.Id] = job;
        job.Status = JobStatus.Queued;

        if (!_channel.Writer.TryWrite(job.Id))
            throw new InvalidOperationException("Kuyruğa yazma başarısız.");

        return Task.FromResult(job.Id);
    }

    public async IAsyncEnumerable<PrintJob> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var id in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            if (_jobs.TryGetValue(id, out var job))
                yield return job;
        }
    }

    public Task<PrintJob?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<bool> RequeueAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var job)) return Task.FromResult(false);

        if (job.Status is not (JobStatus.Failed or JobStatus.Cancelled))
            return Task.FromResult(false);

        job.Status = JobStatus.Queued;
        job.LastError = null;
        job.CompletedAt = null;

        if (!_channel.Writer.TryWrite(job.Id))
            throw new InvalidOperationException("Yeniden kuyruğa yazma başarısız.");

        return Task.FromResult(true);
    }

    public QueueSummary GetSummary()
    {
        int pending = 0, inFlight = 0, failed = 0, succeeded = 0;
        foreach (var job in _jobs.Values)
        {
            switch (job.Status)
            {
                case JobStatus.Queued: pending++; break;
                case JobStatus.Printing: inFlight++; break;
                case JobStatus.Failed: failed++; break;
                case JobStatus.Succeeded: succeeded++; break;
            }
        }

        double? eta = null;
        lock (_etaGate)
        {
            if (_recentDurationsMs.Count > 0 && pending > 0)
            {
                var avg = _recentDurationsMs.Average();
                eta = (pending * avg) / 1000.0;
            }
        }

        return new QueueSummary
        {
            Pending = pending,
            InFlight = inFlight,
            Failed = failed,
            Succeeded = succeeded,
            EtaSeconds = eta
        };
    }

    public void MarkCompleted(PrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_lastCompletedGate) _lastCompleted = job;

        if (job.Status == JobStatus.Succeeded && job.CompletedAt is { } completed)
        {
            var durationMs = (completed - job.CreatedAt).TotalMilliseconds;
            if (durationMs > 0 && durationMs < 60_000)
            {
                lock (_etaGate)
                {
                    if (_recentDurationsMs.Count == EtaWindowSize) _recentDurationsMs.Dequeue();
                    _recentDurationsMs.Enqueue(durationMs);
                }
            }
        }
    }

    public PrintJob? GetLastCompleted()
    {
        lock (_lastCompletedGate) return _lastCompleted;
    }

    public void Rehydrate(PrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        _jobs[job.Id] = job;
    }
}
