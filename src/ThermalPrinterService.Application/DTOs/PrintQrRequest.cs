using System.ComponentModel.DataAnnotations;
using ThermalPrinterService.Application.Receipts;

namespace ThermalPrinterService.Application.DTOs;

/// <summary>
/// /print/qr endpoint gövdesi: tek başına bir QR kod basar.
/// </summary>
public sealed class PrintQrRequest
{
    /// <summary>QR'a kodlanacak metin (URL, JSON, vb.).</summary>
    [Required, StringLength(2048, MinimumLength = 1)]
    public string Data { get; init; } = string.Empty;

    /// <summary>1..16; 4-8 arası okunur boyut.</summary>
    [Range(1, 16)]
    public byte ModuleSize { get; init; } = 6;

    /// <summary>L/M/Q/H — varsayılan M (15%).</summary>
    public ReceiptQrEcc Ecc { get; init; } = ReceiptQrEcc.M;

    /// <summary>QR'ı ortalamak/sola yaslama vb.</summary>
    public ReceiptAlignment Alignment { get; init; } = ReceiptAlignment.Center;
}
