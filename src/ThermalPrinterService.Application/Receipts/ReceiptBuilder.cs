namespace ThermalPrinterService.Application.Receipts;

/// <summary>
/// Fluent builder for ReceiptDocument. Kullanım:
///   var doc = new ReceiptBuilder()
///       .Image(logo)
///       .Text("ACO RECYCLING", align: Center, bold: true, width: 2, height: 2)
///       .Text($"MachineID: {id}", align: Center)
///       .Feed(1)
///       .Table(new[] {"Product","Qty","Reward"}, rows)
///       .Qr(url)
///       .Feed(3)
///       .Cut()
///       .Build();
/// </summary>
public sealed class ReceiptBuilder
{
    private readonly List<ReceiptElement> _elements = new();

    public ReceiptBuilder Text(
        string text,
        ReceiptAlignment align = ReceiptAlignment.Left,
        bool bold = false,
        byte width = 1,
        byte height = 1,
        bool underline = false)
    {
        _elements.Add(new TextElement
        {
            Text = text,
            Alignment = align,
            Bold = bold,
            Width = width,
            Height = height,
            Underline = underline
        });
        return this;
    }

    public ReceiptBuilder Image(byte[] image, ReceiptAlignment align = ReceiptAlignment.Center)
    {
        _elements.Add(new ImageElement { Image = image, Alignment = align });
        return this;
    }

    public ReceiptBuilder Qr(
        string data,
        byte moduleSize = 6,
        ReceiptQrEcc ecc = ReceiptQrEcc.M,
        ReceiptAlignment align = ReceiptAlignment.Center)
    {
        _elements.Add(new QrElement
        {
            Data = data,
            ModuleSize = moduleSize,
            ErrorCorrection = ecc,
            Alignment = align
        });
        return this;
    }

    public ReceiptBuilder Table(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        bool headerBold = true)
    {
        _elements.Add(new TableElement { Headers = headers, Rows = rows, HeaderBold = headerBold });
        return this;
    }

    public ReceiptBuilder Feed(byte lines = 1)
    {
        _elements.Add(new FeedElement { Lines = lines });
        return this;
    }

    public ReceiptBuilder Separator(char character = '-', int width = 0)
    {
        _elements.Add(new SeparatorElement { Character = character, Width = width });
        return this;
    }

    public ReceiptBuilder Cut(byte feedDots = 30)
    {
        _elements.Add(new CutElement { FeedDots = feedDots });
        return this;
    }

    public ReceiptDocument Build() => new() { Elements = new List<ReceiptElement>(_elements) };
}
