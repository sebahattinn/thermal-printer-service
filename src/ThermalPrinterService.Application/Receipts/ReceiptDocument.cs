namespace ThermalPrinterService.Application.Receipts;

public enum ReceiptAlignment : byte { Left = 0, Center = 1, Right = 2 }
public enum ReceiptQrEcc : byte { L = 48, M = 49, Q = 50, H = 51 }

/// <summary>
/// Bir makbuzun ardışık elementlerini tutan basit DTO.
/// Composer bu listeyi sırayla ESC/POS byte zincirine çevirir.
/// </summary>
public sealed class ReceiptDocument
{
    public List<ReceiptElement> Elements { get; init; } = new();
}

public abstract class ReceiptElement { }

public sealed class TextElement : ReceiptElement
{
    public string Text { get; init; } = string.Empty;
    public ReceiptAlignment Alignment { get; init; } = ReceiptAlignment.Left;
    public bool Bold { get; init; }
    public byte Width { get; init; } = 1;   // 1..8
    public byte Height { get; init; } = 1;  // 1..8
    public bool Underline { get; init; }
}

public sealed class ImageElement : ReceiptElement
{
    /// <summary>PNG / JPEG / BMP byte verisi.</summary>
    public byte[] Image { get; init; } = Array.Empty<byte>();
    public ReceiptAlignment Alignment { get; init; } = ReceiptAlignment.Center;
}

public sealed class QrElement : ReceiptElement
{
    public string Data { get; init; } = string.Empty;
    public byte ModuleSize { get; init; } = 6;
    public ReceiptQrEcc ErrorCorrection { get; init; } = ReceiptQrEcc.M;
    public ReceiptAlignment Alignment { get; init; } = ReceiptAlignment.Center;
}

public sealed class TableElement : ReceiptElement
{
    public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<IReadOnlyList<string>>();
    public bool HeaderBold { get; init; } = true;
}

public sealed class FeedElement : ReceiptElement
{
    public byte Lines { get; init; } = 1;
}

public sealed class SeparatorElement : ReceiptElement
{
    public char Character { get; init; } = '-';
    /// <summary>Karakter satırının genişliği (yazıcı kolon sayısı). 0 ise composer'dan miras alır.</summary>
    public int Width { get; init; } = 0;
}

public sealed class CutElement : ReceiptElement
{
    public byte FeedDots { get; init; } = 30;
}
