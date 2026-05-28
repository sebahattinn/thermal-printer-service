namespace ThermalPrinterService.Application.DTOs;

/// <summary>
/// Rulo ömrü snapshot'ı; /status ve /paper endpoint'lerinden döner.
/// Tüm uzunluklar milimetre cinsinden (yazıcı doğal ölçüsü).
/// </summary>
public sealed class PaperInfo
{
    /// <summary>Bu rulodan toplam kullanılan kağıt (mm).</summary>
    public double UsedMm { get; init; }

    /// <summary>Rulonun toplam uzunluğu (mm) — config'ten gelir.</summary>
    public double RollLengthMm { get; init; }

    /// <summary>Kalan kağıt (mm). Negatif olamaz.</summary>
    public double RemainingMm { get; init; }

    /// <summary>Kalan %.  0-100 arası.</summary>
    public double RemainingPercent { get; init; }

    /// <summary>Bu rulodan başarıyla basılan toplam iş sayısı.</summary>
    public int PrintedJobCount { get; init; }

    /// <summary>Job başına ortalama kağıt (mm) — kalan fiş tahminini bundan üretir.</summary>
    public double? AverageJobMm { get; init; }

    /// <summary>Kalan tahmini fiş sayısı (null = henüz ortalama yok).</summary>
    public int? EstimatedJobsRemaining { get; init; }
}
