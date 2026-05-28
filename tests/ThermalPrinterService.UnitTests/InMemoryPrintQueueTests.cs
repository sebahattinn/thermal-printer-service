using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Queueing;

namespace ThermalPrinterService.UnitTests;

public sealed class InMemoryPrintQueueTests
{
    [Fact]
    public async Task Enqueue_then_dequeue_returns_same_job()
    {
        var q = new InMemoryPrintQueue();
        var job = new PrintJob { Kind = JobKind.Text, Text = "hello" };

        var id = await q.EnqueueAsync(job, default);
        Assert.Equal(job.Id, id);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var dequeued in q.DequeueAllAsync(cts.Token))
        {
            Assert.Same(job, dequeued);
            cts.Cancel(); // tek elemandan sonra cik
            break;
        }
    }

    [Fact]
    public async Task GetSummary_counts_jobs_by_status()
    {
        var q = new InMemoryPrintQueue();

        var a = new PrintJob { Kind = JobKind.Text, Text = "a" };
        var b = new PrintJob { Kind = JobKind.Text, Text = "b" };
        var c = new PrintJob { Kind = JobKind.Text, Text = "c" };

        await q.EnqueueAsync(a, default);
        await q.EnqueueAsync(b, default);
        await q.EnqueueAsync(c, default);

        b.Status = JobStatus.Printing;
        c.Status = JobStatus.Failed;

        var s = q.GetSummary();
        Assert.Equal(1, s.Pending);
        Assert.Equal(1, s.InFlight);
        Assert.Equal(1, s.Failed);
    }

    [Fact]
    public async Task Requeue_only_accepts_failed_or_cancelled()
    {
        var q = new InMemoryPrintQueue();

        var queued = new PrintJob { Kind = JobKind.Text, Text = "queued" };
        var printing = new PrintJob { Kind = JobKind.Text, Text = "printing" };
        var succeeded = new PrintJob { Kind = JobKind.Text, Text = "ok" };
        var failed = new PrintJob { Kind = JobKind.Text, Text = "ng" };
        var cancelled = new PrintJob { Kind = JobKind.Text, Text = "x" };

        await q.EnqueueAsync(queued, default);
        await q.EnqueueAsync(printing, default);
        await q.EnqueueAsync(succeeded, default);
        await q.EnqueueAsync(failed, default);
        await q.EnqueueAsync(cancelled, default);

        printing.Status = JobStatus.Printing;
        succeeded.Status = JobStatus.Succeeded;
        failed.Status = JobStatus.Failed;
        cancelled.Status = JobStatus.Cancelled;

        Assert.False(await q.RequeueAsync(queued.Id, default));
        Assert.False(await q.RequeueAsync(printing.Id, default));
        Assert.False(await q.RequeueAsync(succeeded.Id, default));
        Assert.True(await q.RequeueAsync(failed.Id, default));
        Assert.True(await q.RequeueAsync(cancelled.Id, default));

        // Requeue sonrasi job temizlenmeli: status Queued, error/completed reset
        Assert.Equal(JobStatus.Queued, failed.Status);
        Assert.Null(failed.LastError);
        Assert.Null(failed.CompletedAt);
    }

    [Fact]
    public async Task Requeue_unknown_id_returns_false()
    {
        var q = new InMemoryPrintQueue();
        Assert.False(await q.RequeueAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Get_returns_job_by_id()
    {
        var q = new InMemoryPrintQueue();
        var job = new PrintJob { Kind = JobKind.Text, Text = "x" };
        await q.EnqueueAsync(job, default);

        var found = await q.GetAsync(job.Id, default);
        Assert.Same(job, found);
    }
}
