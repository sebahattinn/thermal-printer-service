using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThermalPrinterService.Application.Receipts;
using ThermalPrinterService.Infrastructure.Configuration;
using ThermalPrinterService.Infrastructure.Printers;

namespace ThermalPrinterService.UnitTests;

public sealed class EscPosReceiptComposerTests
{
    private static EscPosReceiptComposer NewComposer(int widthDots = 384) =>
        new(new MonochromeImageEncoder(),
            new TestOptionsMonitor<PrinterOptions>(new PrinterOptions { PrintableWidthDots = widthDots }));

    [Fact]
    public void Compose_starts_with_Init_and_Turkish_CodePage()
    {
        var doc = new ReceiptBuilder().Text("hi").Build();
        var bytes = NewComposer().Compose(doc);

        // ESC @ + ESC t 32 = 1B 40 1B 74 20
        Assert.Equal(0x1B, bytes[0]);
        Assert.Equal(0x40, bytes[1]);
        Assert.Equal(0x1B, bytes[2]);
        Assert.Equal(0x74, bytes[3]);
        Assert.Equal(32, bytes[4]); // CP1254
    }

    [Fact]
    public void Compose_text_emits_alignment_bold_size_and_payload()
    {
        var doc = new ReceiptBuilder()
            .Text("AB", align: ReceiptAlignment.Center, bold: true, width: 2, height: 2)
            .Build();
        var bytes = NewComposer().Compose(doc);

        // İçinde ESC a 1 (center), ESC E 1 (bold), GS ! 0x11 (2x2) ve "AB" geçmeli
        Assert.Contains(new byte[] { 0x1B, 0x61, 0x01 }, b => b == 0x1B); // smoke
        var hex = BitConverter.ToString(bytes);
        Assert.Contains("1B-61-01", hex);  // center alignment
        Assert.Contains("1B-45-01", hex);  // bold on
        Assert.Contains("1D-21-11", hex);  // size 2x2
        Assert.Contains("41-42", hex);     // "AB"
    }

    [Theory]
    [InlineData("Türkçe karakter test: ç ğ ı ş ü ö İ", new byte[] { 0xE7, 0xF0, 0xFD, 0xFE, 0xFC, 0xF6, 0xDD })]
    [InlineData("Français: éèêçàôùîüï", new byte[] { 0xE9, 0xE8, 0xEA, 0xE7, 0xE0, 0xF4, 0xF9, 0xEE, 0xFC, 0xEF })]
    [InlineData("Deutsch: Straße ÄÖÜäöüß", new byte[] { 0xC4, 0xD6, 0xDC, 0xE4, 0xF6, 0xFC, 0xDF })]
    public void Compose_encodes_multilingual_text_in_CP1254(string text, byte[] expectedBytes)
    {
        var doc = new ReceiptBuilder().Text(text).Build();
        var output = NewComposer().Compose(doc);

        // Beklenen CP1254 byte'larının HEPSİ çıktıda bulunmalı
        foreach (var b in expectedBytes)
        {
            Assert.Contains(b, output);
        }
    }

    [Fact]
    public void Compose_replaces_TL_symbol_with_TL_literal()
    {
        var doc = new ReceiptBuilder().Text("Reward: 3.00 ₺").Build();
        var bytes = NewComposer().Compose(doc);

        // ₺ Unicode CP1254'te yok; "TL" literaline çevrilmeli
        var hex = BitConverter.ToString(bytes);
        // "TL" = 0x54 0x4C
        Assert.Contains("54-4C", hex);
    }

    [Fact]
    public void Compose_separator_uses_full_column_width()
    {
        var doc = new ReceiptBuilder().Separator('=').Build();
        var bytes = NewComposer(widthDots: 384).Compose(doc); // 384/12 = 32 kolon

        // "=" karakteri 32 kez ardarda geçmeli (0x3D)
        var hex = BitConverter.ToString(bytes);
        var pattern = string.Join("-", Enumerable.Repeat("3D", 32));
        Assert.Contains(pattern, hex);
    }

    [Fact]
    public void Compose_table_writes_header_separator_and_rows()
    {
        var doc = new ReceiptBuilder()
            .Table(
                new[] { "Product", "Qty", "Reward" },
                new List<IReadOnlyList<string>>
                {
                    new[] { "Glass", "0", "0" },
                    new[] { "Plastic", "2", "2" }
                })
            .Build();
        var bytes = NewComposer().Compose(doc);

        var hex = BitConverter.ToString(bytes);
        Assert.Contains(BitConverter.ToString(System.Text.Encoding.ASCII.GetBytes("Product")), hex);
        Assert.Contains(BitConverter.ToString(System.Text.Encoding.ASCII.GetBytes("Glass")), hex);
        Assert.Contains(BitConverter.ToString(System.Text.Encoding.ASCII.GetBytes("Plastic")), hex);
    }

    [Fact]
    public void Compose_qr_emits_GS_paren_k_chain_ending_with_print_buffer()
    {
        var doc = new ReceiptBuilder().Qr("https://aco.test/r/1").Build();
        var bytes = NewComposer().Compose(doc);

        // QR son komutu Print Buffer: 1D 28 6B 03 00 31 51 30
        var hex = BitConverter.ToString(bytes);
        Assert.Contains("1D-28-6B-03-00-31-51-30", hex);
        // QR ilk komutu Select Model: 1D 28 6B 04 00 31 41
        Assert.Contains("1D-28-6B-04-00-31-41", hex);
    }

    [Fact]
    public void Compose_image_writes_alignment_then_GS_v_0_then_LF()
    {
        // 16x16 black PNG
        using var img = new Image<Rgba32>(16, 16);
        img.Mutate(c => c.BackgroundColor(new Rgba32(0, 0, 0, 255)));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);

        var doc = new ReceiptBuilder().Image(ms.ToArray()).Build();
        var bytes = NewComposer().Compose(doc);

        var hex = BitConverter.ToString(bytes);
        // Center alignment + GS v 0 header (1D-76-30-00)
        Assert.Contains("1B-61-01", hex);
        Assert.Contains("1D-76-30-00", hex);
    }

    [Fact]
    public void Compose_cut_emits_feed_and_cut_GS_V_66_n()
    {
        var doc = new ReceiptBuilder().Cut(feedDots: 10).Build();
        var bytes = NewComposer().Compose(doc);

        var hex = BitConverter.ToString(bytes);
        Assert.Contains("1D-56-42-0A", hex); // GS V 66 10
    }

    [Fact]
    public void Compose_ACO_like_receipt_produces_nonempty_payload_with_all_features()
    {
        // ACO fişinin küçük bir taklidi: text + table + QR + cut
        using var logo = new Image<Rgba32>(32, 32);
        logo.Mutate(c => c.BackgroundColor(new Rgba32(0, 0, 0, 255)));
        using var ms = new MemoryStream();
        logo.SaveAsPng(ms);

        var doc = new ReceiptBuilder()
            .Image(ms.ToArray())
            .Text("ACO RECYCLING", align: ReceiptAlignment.Center, bold: true)
            .Text("MachineID: ACO-TEST-0001", align: ReceiptAlignment.Center)
            .Text("16 Eylül 2025 16:19:02 UTC", align: ReceiptAlignment.Center)
            .Text("Reward: 3.00 ₺", align: ReceiptAlignment.Center, width: 2, height: 2)
            .Separator()
            .Table(
                new[] { "Product", "Qty", "Reward" },
                new List<IReadOnlyList<string>>
                {
                    new[] { "Glass", "0", "0" },
                    new[] { "Plastic", "2", "2" },
                    new[] { "Metal", "1", "1" }
                })
            .Feed(2)
            .Qr("https://aco.test/r/ACO-TEST-0001-0001")
            .Feed(3)
            .Cut()
            .Build();

        var bytes = NewComposer().Compose(doc);

        // Sıhhat kontrolleri
        Assert.True(bytes.Length > 500); // tüm element + raster + QR makul boyut
        var hex = BitConverter.ToString(bytes);

        // Init + codepage
        Assert.StartsWith("1B-40-1B-74-20", hex);
        // Raster header
        Assert.Contains("1D-76-30-00", hex);
        // QR print buffer
        Assert.Contains("1D-28-6B-03-00-31-51-30", hex);
        // Cut
        Assert.Contains("1D-56-42", hex);
        // ₺ → TL fallback
        Assert.Contains("54-4C", hex);
    }
}
