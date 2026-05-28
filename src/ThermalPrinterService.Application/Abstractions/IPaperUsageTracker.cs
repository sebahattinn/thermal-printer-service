using ThermalPrinterService.Application.DTOs;
using ThermalPrinterService.Domain.Entities;

namespace ThermalPrinterService.Application.Abstractions;

/// <summary>
/// Yazıcının kullandığı kağıt miktarını izler ve kalan rulo ömrünü tahmin eder.
/// Üretim ortamında operatör rulosu değiştirdiğinde <see cref="ResetRoll"/> çağrılır.
/// </summary>
public interface IPaperUsageTracker
{
    /// <summary>Job başarıyla basıldıktan sonra kullanılan mm'yi tahmin edip toplama ekler.</summary>
    void RegisterPrinted(PrintJob job);

    /// <summary>Yeni rulo takıldı; sayaçları sıfırla.</summary>
    void ResetRoll();

    /// <summary>Anlık snapshot: toplam kullanım, kalan, yüzde, ortalama fiş başına mm, kalan fiş sayısı.</summary>
    PaperInfo GetInfo();
}
