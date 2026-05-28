using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThermalPrinterService.Infrastructure.Printers;

namespace ThermalPrinterService.UnitTests;

public sealed class MonochromeImageEncoderTests
{
    /// <summary>Bellekte verilen renkte tek-renk PNG üretir (test fixturesı).</summary>
    private static byte[] MakeSolidPng(int width, int height, byte r, byte g, byte b)
    {
        using var img = new Image<Rgba32>(width, height);
        img.Mutate(c => c.BackgroundColor(new Rgba32(r, g, b, 255)));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Encode_starts_with_GS_v_0_header()
    {
        var png = MakeSolidPng(64, 16, 0, 0, 0); // tamamen siyah 64x16
        var enc = new MonochromeImageEncoder();

        var bytes = enc.Encode(png, maxWidthDots: 384);

        Assert.Equal(0x1D, bytes[0]);
        Assert.Equal(0x76, bytes[1]);
        Assert.Equal(0x30, bytes[2]);
        Assert.Equal(0x00, bytes[3]); // m = normal
    }

    [Fact]
    public void Encode_writes_correct_width_in_bytes_and_height_in_dots()
    {
        var png = MakeSolidPng(64, 16, 0, 0, 0); // 64 dot = 8 byte genişlik, 16 dot yükseklik
        var enc = new MonochromeImageEncoder();

        var bytes = enc.Encode(png, maxWidthDots: 384);

        var xL = bytes[4];
        var xH = bytes[5];
        var yL = bytes[6];
        var yH = bytes[7];
        Assert.Equal(8, xL + (xH << 8));
        Assert.Equal(16, yL + (yH << 8));

        // header(8) + 8 byte/satır × 16 satır = 8 + 128 = 136
        Assert.Equal(136, bytes.Length);
    }

    [Fact]
    public void Encode_all_black_image_sets_every_bit()
    {
        var png = MakeSolidPng(8, 4, 0, 0, 0);
        var enc = new MonochromeImageEncoder();
        var bytes = enc.Encode(png, 384);

        // header sonrası 4 byte (4 satır × 1 byte) hepsi 0xFF olmalı
        Assert.Equal(0xFF, bytes[8]);
        Assert.Equal(0xFF, bytes[9]);
        Assert.Equal(0xFF, bytes[10]);
        Assert.Equal(0xFF, bytes[11]);
    }

    [Fact]
    public void Encode_all_white_image_sets_no_bits()
    {
        var png = MakeSolidPng(8, 4, 255, 255, 255);
        var enc = new MonochromeImageEncoder();
        var bytes = enc.Encode(png, 384);

        Assert.Equal(0x00, bytes[8]);
        Assert.Equal(0x00, bytes[9]);
        Assert.Equal(0x00, bytes[10]);
        Assert.Equal(0x00, bytes[11]);
    }

    [Fact]
    public void Encode_scales_down_to_max_width_when_image_is_wider()
    {
        // 800 dot genişliğindeki bir görsel, maxWidthDots=384'e ölçeklenmeli
        var png = MakeSolidPng(800, 100, 0, 0, 0);
        var enc = new MonochromeImageEncoder();

        var bytes = enc.Encode(png, maxWidthDots: 384);

        var xL = bytes[4];
        var xH = bytes[5];
        var bytesPerRow = xL + (xH << 8);

        // bytesPerRow * 8 <= 384 olmalı
        Assert.True(bytesPerRow * 8 <= 384);
        // 800'den küçük olmalı (ölçeklendi)
        Assert.True(bytesPerRow * 8 < 800);
    }

    [Fact]
    public void Encode_rounds_width_down_to_multiple_of_8()
    {
        // 50 dot genişlik → 48'e indirilmeli (50 / 8 = 6 byte = 48 dot)
        var png = MakeSolidPng(50, 10, 0, 0, 0);
        var enc = new MonochromeImageEncoder();
        var bytes = enc.Encode(png, 384);

        var bytesPerRow = bytes[4] + (bytes[5] << 8);
        Assert.Equal(6, bytesPerRow); // 50 → 48 dot → 6 byte
    }

    [Fact]
    public void Encode_rejects_empty_input()
    {
        var enc = new MonochromeImageEncoder();
        Assert.Throws<ArgumentException>(() => enc.Encode(Array.Empty<byte>(), 384));
    }

    [Fact]
    public void Encode_msb_first_bit_packing_for_single_black_pixel_at_x0()
    {
        // 8x1 görsel, sadece x=0 piksel siyah olsun
        using var img = new Image<Rgba32>(8, 1);
        img[0, 0] = new Rgba32(0, 0, 0, 255);
        for (var x = 1; x < 8; x++) img[x, 0] = new Rgba32(255, 255, 255, 255);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);

        var enc = new MonochromeImageEncoder();
        var bytes = enc.Encode(ms.ToArray(), 384);

        // ESC/POS MSB-first: x=0 → bit 7 (0x80) set olmalı
        Assert.Equal(0x80, bytes[8]);
    }
}
