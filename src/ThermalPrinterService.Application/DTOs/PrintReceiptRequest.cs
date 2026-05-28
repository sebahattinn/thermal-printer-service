using System.ComponentModel.DataAnnotations;
using ThermalPrinterService.Application.Receipts;

namespace ThermalPrinterService.Application.DTOs;

/// <summary>
/// /print/receipt endpoint JSON gövdesi. Element listesi sırayla işlenir.
/// </summary>
public sealed class PrintReceiptRequest
{
    [Required, MinLength(1)]
    public List<ReceiptElementDto> Elements { get; init; } = new();
}

/// <summary>
/// Tek bir polymorphic element. Type alanına göre diğer alanlar yorumlanır.
/// Geçersiz birleşim controller seviyesinde reddedilir.
/// </summary>
public sealed class ReceiptElementDto
{
    [Required]
    public string Type { get; init; } = string.Empty; // text|image|qr|table|feed|separator|cut

    // Text
    public string? Text { get; init; }
    public ReceiptAlignment Alignment { get; init; } = ReceiptAlignment.Left;
    public bool Bold { get; init; }
    public byte Width { get; init; } = 1;
    public byte Height { get; init; } = 1;
    public bool Underline { get; init; }

    // Image
    public string? ImageBase64 { get; init; }

    // Qr
    public string? QrData { get; init; }
    public byte QrModuleSize { get; init; } = 6;
    public ReceiptQrEcc QrEcc { get; init; } = ReceiptQrEcc.M;

    // Table
    public List<string>? Headers { get; init; }
    public List<List<string>>? Rows { get; init; }
    public bool HeaderBold { get; init; } = true;

    // Feed
    public byte Lines { get; init; } = 1;

    // Separator
    public string? Character { get; init; }

    // Cut
    public byte FeedDots { get; init; } = 30;
}
