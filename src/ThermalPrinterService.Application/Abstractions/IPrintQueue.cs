using ThermalPrinterService.Application.DTOs;
using ThermalPrinterService.Domain.Entities;

namespace ThermalPrinterService.Application.Abstractions;

public interface IPrintQueue
{
    Task<Guid> EnqueueAsync(PrintJob job, CancellationToken cancellationToken);
    IAsyncEnumerable<PrintJob> DequeueAllAsync(CancellationToken cancellationToken);

    Task<PrintJob?> GetAsync(Guid jobId, CancellationToken cancellationToken);
    Task<bool> RequeueAsync(Guid jobId, CancellationToken cancellationToken);

    QueueSummary GetSummary();

    /// <summary>Worker tarafindan job tamamlandiginda cagrilir (succeeded veya failed).</summary>
    void MarkCompleted(PrintJob job);

    /// <summary>Son tamamlanan/basarisiz job (null = henuz hicbir is tamamlanmadi).</summary>
    PrintJob? GetLastCompleted();

    /// <summary>
    /// Startup'ta diskten yuklenen failed jobs'i queue dict'e dahil eder ki
    /// /reprint cagrisinda bulunabilsin. Queue'ya enqueue ETMEZ.
    /// </summary>
    void Rehydrate(PrintJob job);
}
