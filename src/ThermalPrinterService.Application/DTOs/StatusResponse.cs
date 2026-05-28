using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Application.DTOs;

public sealed class StatusResponse
{
    public bool Connected { get; init; }
    public ConnectionMode? Mode { get; init; }
    public PrinterState State { get; init; }
    public QueueSummary Queue { get; init; } = new();
    public LastJobInfo? LastJob { get; init; }
    public PaperInfo? Paper { get; init; }
}

public sealed class QueueSummary
{
    public int Pending { get; init; }
    public int InFlight { get; init; }
    public int Failed { get; init; }
    public int Succeeded { get; init; }

    /// <summary>Kuyruktaki bekleyen islerin tahmini bitirme suresi (saniye); ETA hesaplanmamissa null.</summary>
    public double? EtaSeconds { get; init; }
}

/// <summary>
/// Servis taraf 抗ndan tutulan son tamamlanan veya basarisiz job ozeti.
/// UI buradan "son is" bilgisini gosterir.
/// </summary>
public sealed class LastJobInfo
{
    public Guid JobId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public JobStatus Status { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int Attempts { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }
}
