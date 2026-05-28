using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Configuration;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// Bir PrintJob'un yaklaşık kaç mm kağıt harcadığını tahmin eder.
/// - Text: satır sayısı × satır yüksekliği
/// - Image: konfigürasyondaki ortalama görsel yüksekliği
/// - Raw (composer çıktısı): LF byte sayısı × satır yüksekliği + içindeki GS v 0
///   raster bitmap header'larından okunan y boyutları (8 dot = 1 mm).
/// Her durumda kesim payı eklenir.
/// </summary>
public static class PaperUsageEstimator
{
    public static double EstimateMm(PrintJob job, PaperOptions opts)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(opts);

        return job.Kind switch
        {
            JobKind.Text => EstimateText(job.Text ?? string.Empty, opts),
            JobKind.Image => opts.AverageImageHeightMm + opts.CutOverheadMm,
            JobKind.Raw => EstimateRaw(job.RawBytes ?? Array.Empty<byte>(), opts),
            _ => opts.CutOverheadMm
        };
    }

    private static double EstimateText(string text, PaperOptions opts)
    {
        // Satır = '\n' + 1 (boş metin = 1 satır kesim için).
        var lines = 1 + text.Count(c => c == '\n');
        return lines * opts.AverageLineHeightMm + opts.CutOverheadMm;
    }

    private static double EstimateRaw(byte[] payload, PaperOptions opts)
    {
        if (payload.Length == 0) return opts.CutOverheadMm;

        // 1) LF (0x0A) byte sayısı × satır yüksekliği
        var lfCount = 0;
        foreach (var b in payload) if (b == 0x0A) lfCount++;
        var textLinesMm = lfCount * opts.AverageLineHeightMm;

        // 2) GS v 0 raster bitmap header'larını tara: 1D 76 30 m xL xH yL yH
        var rasterMm = 0.0;
        for (var i = 0; i < payload.Length - 7; i++)
        {
            if (payload[i] == 0x1D && payload[i + 1] == 0x76 && payload[i + 2] == 0x30)
            {
                var yL = payload[i + 6];
                var yH = payload[i + 7];
                var heightDots = yL + (yH << 8);
                rasterMm += heightDots / 8.0; // 8 dot = 1 mm @ 203dpi
                i += 7; // header'ı atla
            }
        }

        return textLinesMm + rasterMm + opts.CutOverheadMm;
    }
}
