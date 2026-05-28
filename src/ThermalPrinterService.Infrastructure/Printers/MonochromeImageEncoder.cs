using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThermalPrinterService.Application.Abstractions;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// PNG/JPEG/BMP'yi gri tona çevirip eşik (threshold) ile 1bpp monochrome bitmap'e
/// indirir ve ESC/POS GS v 0 raster komut zincirini üretir.
/// Genişlik 8'in katına yuvarlanır; yazıcının max genişliğini aşan görsel
/// oranı bozulmadan ölçeklenir.
/// </summary>
public sealed class MonochromeImageEncoder : IImageEncoder
{
    // 0..255 arası gri eşik. < threshold => siyah (1), >= => beyaz (0).
    // 0.5 nötr; logo'larda 0.6 daha temiz çıkar, ama oranı kullanıcı isterse override etsin.
    public byte Threshold { get; init; } = 128;

    public byte[] Encode(ReadOnlySpan<byte> imageBytes, int maxWidthDots)
    {
        if (imageBytes.IsEmpty) throw new ArgumentException("Görsel boş.", nameof(imageBytes));
        if (maxWidthDots < 8) throw new ArgumentOutOfRangeException(nameof(maxWidthDots));

        // ImageSharp Load: byte[] ister (Span overload yok), kopya zorunlu.
        var inputArray = imageBytes.ToArray();
        using var image = Image.Load<Rgba32>(inputArray);

        // Hedef genişlik: maxWidthDots'tan büyük değil, 8'in katı.
        var targetWidth = Math.Min(image.Width, maxWidthDots);
        targetWidth -= targetWidth % 8;
        if (targetWidth == 0) targetWidth = 8;

        // Oran korunarak yeniden boyutlandır.
        image.Mutate(ctx =>
        {
            if (image.Width != targetWidth)
            {
                ctx.Resize(targetWidth, 0); // 0 -> oranla otomatik yükseklik
            }
            ctx.Grayscale();
        });

        var width = image.Width;
        var height = image.Height;
        var bytesPerRow = width / 8;

        // 1bpp paketli buffer
        var packed = new byte[bytesPerRow * height];

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var rowOffset = y * bytesPerRow;

                for (var x = 0; x < width; x++)
                {
                    // Grayscale sonrası R==G==B, herhangi birini al
                    var gray = row[x].R;
                    if (gray < Threshold)
                    {
                        // siyah piksel => bit set (1)
                        var bytePos = rowOffset + (x / 8);
                        var bitPos = 7 - (x % 8); // MSB-first (ESC/POS spec)
                        packed[bytePos] |= (byte)(1 << bitPos);
                    }
                }
            }
        });

        return BuildGsV0(bytesPerRow, height, packed);
    }

    /// <summary>
    /// GS v 0 m xL xH yL yH d1..dk raster bitmap komutunu paketler (KP-300 manuel sayfa 39).
    /// m=0 (normal), xL+xH*256 = bytesPerRow, yL+yH*256 = height dot.
    /// </summary>
    private static byte[] BuildGsV0(int bytesPerRow, int height, byte[] packed)
    {
        // ESC/POS max height: yL+yH*256 = 65535 dot — fiş yazıcı için fazlasıyla yeterli.
        if (height > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(height),
                $"Görsel yükseklik {ushort.MaxValue} dot'u aşamaz.");

        var xL = (byte)(bytesPerRow & 0xFF);
        var xH = (byte)((bytesPerRow >> 8) & 0xFF);
        var yL = (byte)(height & 0xFF);
        var yH = (byte)((height >> 8) & 0xFF);

        var header = new byte[] { 0x1D, 0x76, 0x30, 0x00, xL, xH, yL, yH };
        var output = new byte[header.Length + packed.Length];
        header.CopyTo(output, 0);
        packed.CopyTo(output, header.Length);
        return output;
    }
}
