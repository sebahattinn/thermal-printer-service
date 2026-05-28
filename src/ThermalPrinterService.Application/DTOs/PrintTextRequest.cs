using System.ComponentModel.DataAnnotations;

namespace ThermalPrinterService.Application.DTOs;

public sealed class PrintTextRequest
{
    [Required, StringLength(4096, MinimumLength = 1)]
    public string Text { get; init; } = string.Empty;
}

public sealed class PrintImageRequest
{
    // Base64 ile gönderilen görsel; Controller içinde decode edilir.
    [Required]
    public string ImageBase64 { get; init; } = string.Empty;
}

public sealed class EnqueuedResponse
{
    public Guid JobId { get; init; }
    public string Status { get; init; } = "queued";
}
