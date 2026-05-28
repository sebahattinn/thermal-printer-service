using System.Text;
using Microsoft.Extensions.Options;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Application.Receipts;
using ThermalPrinterService.Infrastructure.Configuration;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// ReceiptDocument'i tek bir ESC/POS byte zincirine kompoze eder.
/// Türkçe için CP1254 (Windows-1254) kullanılır; "₺" gibi ESC/POS karşılığı
/// olmayan karakterler güvenli fallback'lere çevrilir (ör. "₺" -> "TL").
/// </summary>
public sealed class EscPosReceiptComposer : IReceiptComposer
{
    private readonly IImageEncoder _imageEncoder;
    private readonly IOptionsMonitor<PrinterOptions> _options;
    private readonly Encoding _cp1254;

    // ESC/POS karşılığı olmayan Unicode karakterler için sadeleştirme tablosu.
    // CP1254'te ₺ yok; € ve $ var ama TR fişlerinde TL kısaltması daha yaygın.
    private static readonly Dictionary<string, string> _charFallbacks = new(StringComparer.Ordinal)
    {
        ["₺"] = "TL",
        ["€"] = "EUR",
        ["—"] = "-",
        ["–"] = "-",
        ["’"] = "'",
        ["‘"] = "'",
        ["“"] = "\"",
        ["”"] = "\""
    };

    public EscPosReceiptComposer(IImageEncoder imageEncoder, IOptionsMonitor<PrinterOptions> options)
    {
        _imageEncoder = imageEncoder;
        _options = options;

        // .NET Core/8: CP1254 default registered değil, manuel kayıt zorunlu.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _cp1254 = Encoding.GetEncoding(1254);
    }

    public byte[] Compose(ReceiptDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var printerWidthDots = _options.CurrentValue.PrintableWidthDots;
        if (printerWidthDots < 8) throw new InvalidOperationException(
            $"PrinterOptions.PrintableWidthDots ({printerWidthDots}) en az 8 olmalı.");

        using var ms = new MemoryStream();

        // Init + Türkçe codepage
        Write(ms, EscPosCommands.Init);
        Write(ms, EscPosCommands.SetCodePage(32)); // 32 = CP1254 / Turkish (KP-300 manuel sayfa 36)

        // Yazıcı kolon sayısı (yaklaşık). Font A (12 dot karakter), genişlik / 12.
        // KP-300: 384/12 = 32 kolon, KP-302: 576/12 = 48 kolon.
        var columnCount = Math.Max(16, printerWidthDots / 12);

        foreach (var elem in document.Elements)
        {
            switch (elem)
            {
                case TextElement t: WriteText(ms, t); break;
                case ImageElement i: WriteImage(ms, i, printerWidthDots); break;
                case QrElement q: WriteQr(ms, q); break;
                case TableElement tb: WriteTable(ms, tb, columnCount); break;
                case FeedElement f: Write(ms, EscPosCommands.FeedLines(f.Lines)); break;
                case SeparatorElement s: WriteSeparator(ms, s, columnCount); break;
                case CutElement c: Write(ms, EscPosCommands.FeedAndCut(c.FeedDots)); break;
            }
        }

        return ms.ToArray();
    }

    // ---- Element işleyiciler ----

    private void WriteText(MemoryStream ms, TextElement t)
    {
        Write(ms, EscPosCommands.SetAlignment((EscPosCommands.Alignment)(byte)t.Alignment));
        Write(ms, EscPosCommands.SetBold(t.Bold));
        Write(ms, EscPosCommands.SetUnderline(t.Underline
            ? EscPosCommands.UnderlineMode.OneDot
            : EscPosCommands.UnderlineMode.Off));
        Write(ms, EscPosCommands.SetTextSize(t.Width, t.Height));

        Write(ms, EncodeText(t.Text));
        Write(ms, EscPosCommands.LineFeed);

        // Sonraki elemana taşmasın diye boyut ve bold'u resetle.
        Write(ms, EscPosCommands.SetTextSize(1, 1));
        Write(ms, EscPosCommands.SetBold(false));
        Write(ms, EscPosCommands.SetUnderline(EscPosCommands.UnderlineMode.Off));
    }

    private void WriteImage(MemoryStream ms, ImageElement i, int printerWidthDots)
    {
        Write(ms, EscPosCommands.SetAlignment((EscPosCommands.Alignment)(byte)i.Alignment));
        Write(ms, _imageEncoder.Encode(i.Image, printerWidthDots));
        Write(ms, EscPosCommands.LineFeed);
    }

    private void WriteQr(MemoryStream ms, QrElement q)
    {
        Write(ms, EscPosCommands.SetAlignment((EscPosCommands.Alignment)(byte)q.Alignment));
        Write(ms, QrCommands.Build(q.Data, q.ModuleSize, (QrErrorCorrection)(byte)q.ErrorCorrection));
        Write(ms, EscPosCommands.LineFeed);
    }

    private void WriteSeparator(MemoryStream ms, SeparatorElement s, int columnCount)
    {
        var width = s.Width > 0 ? s.Width : columnCount;
        var line = new string(s.Character, width);
        Write(ms, EscPosCommands.SetAlignment(EscPosCommands.Alignment.Left));
        Write(ms, EncodeText(line));
        Write(ms, EscPosCommands.LineFeed);
    }

    private void WriteTable(MemoryStream ms, TableElement t, int columnCount)
    {
        if (t.Headers.Count == 0) return;
        var colCount = t.Headers.Count;
        // Eşit genişlikte kolonlar; daha akıllı ölçüm gerekirse veri uzunluğuna bakılabilir.
        var colWidth = Math.Max(4, columnCount / colCount);

        // Header
        Write(ms, EscPosCommands.SetAlignment(EscPosCommands.Alignment.Left));
        if (t.HeaderBold) Write(ms, EscPosCommands.SetBold(true));
        Write(ms, EncodeText(FormatRow(t.Headers, colWidth, columnCount)));
        Write(ms, EscPosCommands.LineFeed);
        if (t.HeaderBold) Write(ms, EscPosCommands.SetBold(false));

        // Ayırıcı
        Write(ms, EncodeText(new string('-', columnCount)));
        Write(ms, EscPosCommands.LineFeed);

        // Rows
        foreach (var row in t.Rows)
        {
            Write(ms, EncodeText(FormatRow(row, colWidth, columnCount)));
            Write(ms, EscPosCommands.LineFeed);
        }
    }

    private static string FormatRow(IReadOnlyList<string> cells, int colWidth, int totalWidth)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < cells.Count; i++)
        {
            var s = cells[i] ?? string.Empty;
            // Kolonu sabit genişlikte sola yasla; taşarsa kıs.
            if (s.Length > colWidth - 1)
                s = s[..(colWidth - 1)];
            sb.Append(s.PadRight(colWidth));
        }

        var output = sb.ToString();
        if (output.Length > totalWidth) output = output[..totalWidth];
        return output;
    }

    // ---- Encoding ve I/O yardımcıları ----

    private byte[] EncodeText(string text)
    {
        var normalised = NormaliseUnsupportedChars(text);
        return _cp1254.GetBytes(normalised);
    }

    private static string NormaliseUnsupportedChars(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var (from, to) in _charFallbacks)
        {
            if (text.Contains(from, StringComparison.Ordinal))
                text = text.Replace(from, to, StringComparison.Ordinal);
        }
        return text;
    }

    private static void Write(MemoryStream ms, ReadOnlySpan<byte> bytes) => ms.Write(bytes);
    private static void Write(MemoryStream ms, byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
}
