using System.Text.Json.Serialization;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Domain.Entities;

// Sema (brief 4. madde): {ts, op, conn, jobId, status, error:{code, detail}}
public sealed class LogEntry
{
    [JsonPropertyName("ts")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("op")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("conn")]
    public ConnectionMode? Connection { get; init; }

    [JsonPropertyName("jobId")]
    public Guid? JobId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("error")]
    public LogError? Error { get; init; }
}

/// <summary>
/// Yapilandirilmis hata yuku. code = makina-okuyabilir sabit (LogErrorCodes),
/// detail = insan-okuyabilir kisa aciklama (stack trace degil).
/// </summary>
public sealed class LogError
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;

    public LogError() { }
    public LogError(string code, string detail) { Code = code; Detail = detail; }
}

/// <summary>
/// Servis genelinde kullanilan log hata kodlari. UI bunlara gore renkli banner gosterir.
/// </summary>
public static class LogErrorCodes
{
    // Donanim hatalari (printer state)
    public const string PaperOut = "PAPER_OUT";
    public const string PaperJam = "PAPER_JAM";
    public const string CoverOpen = "COVER_OPEN";
    public const string Overheat = "OVERHEAT";
    public const string CommError = "COMM_ERROR";
    public const string UnknownCommand = "UNKNOWN_COMMAND";

    // Servis hatalari
    public const string NoConnection = "NO_CONNECTION";
    public const string InvalidInput = "INVALID_INPUT";
    public const string InvalidImage = "INVALID_IMAGE";
    public const string JobNotFound = "JOB_NOT_FOUND";
    public const string ComposeFailed = "COMPOSE_FAILED";
    public const string ConnectFailed = "CONNECT_FAILED";
    public const string PrintFailed = "PRINT_FAILED";

    /// <summary>PrinterState -> error code haritalama (Worker tarafindan kullanilir).</summary>
    public static string FromPrinterState(PrinterState state) => state switch
    {
        PrinterState.PaperOut => PaperOut,
        PrinterState.PaperJam => PaperJam,
        PrinterState.CoverOpen => CoverOpen,
        PrinterState.Overheat => Overheat,
        PrinterState.CommError => CommError,
        PrinterState.UnknownCommand => UnknownCommand,
        _ => PrintFailed
    };
}
