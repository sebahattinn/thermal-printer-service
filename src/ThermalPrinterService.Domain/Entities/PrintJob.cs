using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Domain.Entities;

public sealed class PrintJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public JobKind Kind { get; init; }
    public string? Text { get; init; }
    public byte[]? ImageBytes { get; init; }

    // Receipt veya başka kompozit basımlar için tam ESC/POS payload.
    public byte[]? RawBytes { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastError { get; set; }
}
