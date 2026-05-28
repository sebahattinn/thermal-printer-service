namespace ThermalPrinterService.Application.Abstractions;

/// <summary>
/// Idempotency-Key header'ı için key→jobId eşlemesi. Aynı key/TTL içinde tekrar
/// gelen istek, daha önce üretilen jobId'yi geri döner; iki kez basım yapılmaz.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Verilen key zaten varsa kaydedilmiş jobId'yi döner; yoksa <paramref name="factory"/>
    /// ile yeni jobId üretir, kaydeder ve döner.
    /// Thread-safe; aynı anda iki istek gelirse yalnız biri factory'yi çağırır.
    /// </summary>
    Task<Guid> GetOrAddAsync(string key, Func<Task<Guid>> factory, CancellationToken cancellationToken);
}
