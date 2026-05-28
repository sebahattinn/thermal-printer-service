using ThermalPrinterService.Domain.Entities;

namespace ThermalPrinterService.Application.Abstractions;

/// <summary>
/// Brief 3. madde: "Basılmayan görseller kaydedilmeli."
/// Başarısız basım işlerini kalıcı bir yere (varsayılan: disk) yazar; servis
/// restart edildikten sonra bile /reprint ile tekrar tetiklenebilirler.
/// Job başarılı olunca disk'ten temizlenir.
/// </summary>
public interface IFailedJobArchive
{
    Task SaveAsync(PrintJob job, CancellationToken cancellationToken);
    Task DeleteAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PrintJob>> LoadAllAsync(CancellationToken cancellationToken);
}
