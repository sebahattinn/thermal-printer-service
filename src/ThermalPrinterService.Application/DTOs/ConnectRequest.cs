using System.ComponentModel.DataAnnotations;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Application.DTOs;

public sealed class ConnectRequest
{
    [Required]
    public ConnectionMode Mode { get; init; }
}

public sealed class ConnectResponse
{
    public bool Connected { get; init; }
    public ConnectionMode Mode { get; init; }
    public string Message { get; init; } = string.Empty;
}
