using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Configuration;
using ThermalPrinterService.Infrastructure.Printers;

namespace ThermalPrinterService.UnitTests;

public sealed class PaperUsageEstimatorTests
{
    private static PaperOptions DefaultOpts() => new()
    {
        RollLengthMeters = 80,
        AverageLineHeightMm = 3,
        AverageImageHeightMm = 40,
        CutOverheadMm = 5
    };

    [Fact]
    public void Text_single_line_uses_one_line_height_plus_cut_overhead()
    {
        var job = new PrintJob { Kind = JobKind.Text, Text = "hello" };
        var mm = PaperUsageEstimator.EstimateMm(job, DefaultOpts());
        // 1 satır × 3mm + 5mm cut = 8mm
        Assert.Equal(8.0, mm);
    }

    [Fact]
    public void Text_multiple_lines_uses_lf_count_plus_one()
    {
        var job = new PrintJob { Kind = JobKind.Text, Text = "a\nb\nc" };
        var mm = PaperUsageEstimator.EstimateMm(job, DefaultOpts());
        // 3 satır × 3mm + 5mm cut = 14mm
        Assert.Equal(14.0, mm);
    }

    [Fact]
    public void Image_uses_average_image_height_plus_cut()
    {
        var job = new PrintJob { Kind = JobKind.Image, ImageBytes = new byte[100] };
        var mm = PaperUsageEstimator.EstimateMm(job, DefaultOpts());
        // 40 + 5 = 45mm
        Assert.Equal(45.0, mm);
    }

    [Fact]
    public void Raw_counts_LF_bytes_for_text_lines()
    {
        // 5 LF byte = 5 satır × 3mm + 5mm cut = 20mm
        var payload = new byte[] { 0x41, 0x0A, 0x42, 0x0A, 0x43, 0x0A, 0x44, 0x0A, 0x45, 0x0A };
        var job = new PrintJob { Kind = JobKind.Raw, RawBytes = payload };
        var mm = PaperUsageEstimator.EstimateMm(job, DefaultOpts());
        Assert.Equal(20.0, mm);
    }

    [Fact]
    public void Raw_extracts_GS_v_0_raster_height()
    {
        // GS v 0 m=0, xL=8 xH=0 (8 bytes wide), yL=80 yH=0 (80 dot height = 10mm)
        // Sonra 8*80=640 byte data; bizim için sadece header önemli.
        var header = new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x08, 0x00, 0x50, 0x00 };
        var payload = new byte[header.Length + 640];
        header.CopyTo(payload, 0);

        var job = new PrintJob { Kind = JobKind.Raw, RawBytes = payload };
        var mm = PaperUsageEstimator.EstimateMm(job, DefaultOpts());

        // 80 dot / 8 = 10mm raster + 0 LF + 5mm cut = 15mm
        Assert.Equal(15.0, mm);
    }

    [Fact]
    public void Raw_combines_text_lines_and_raster_height()
    {
        var header = new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x08, 0x00, 0x80, 0x00 }; // 128 dot = 16mm
        var payload = new byte[header.Length + 0x0A.GetHashCode() % 1 + 4];
        header.CopyTo(payload, 0);
        // 3 LF byte ekleyelim
        payload[header.Length] = 0x0A;
        payload[header.Length + 1] = 0x0A;
        payload[header.Length + 2] = 0x0A;
        payload[header.Length + 3] = 0x41;

        var job = new PrintJob { Kind = JobKind.Raw, RawBytes = payload };
        var mm = PaperUsageEstimator.EstimateMm(job, DefaultOpts());

        // 16mm raster + 3 satır × 3mm + 5mm cut = 30mm
        Assert.Equal(30.0, mm);
    }

    [Fact]
    public void Empty_raw_payload_returns_only_cut_overhead()
    {
        var job = new PrintJob { Kind = JobKind.Raw, RawBytes = Array.Empty<byte>() };
        var mm = PaperUsageEstimator.EstimateMm(job, DefaultOpts());
        Assert.Equal(5.0, mm);
    }
}
