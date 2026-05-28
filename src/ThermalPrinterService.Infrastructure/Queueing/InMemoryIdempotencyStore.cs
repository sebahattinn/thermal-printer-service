using System.Collections.Concurrent;
using ThermalPrinterService.Application.Abstractions;

namespace ThermalPrinterService.Infrastructure.Queueing;

/// <summary>
/// In-memory key→jobId eşlemesi + TTL temizliği. Aynı key ile yarış halinde
/// factory yalnız bir kez çağrılır (Lazy + ExecutionAndPublication garantisi).
/// Üretim için Redis/SQLite tabanlı bir backend tercih edilebilir; bu sınıf
/// IIdempotencyStore arayüzü arkasında olduğu için drop-in replace kolaydır.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private sealed record Entry(Lazy<Task<Guid>> Job, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _map = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;

    private const int CleanupBatch = 64;

    public InMemoryIdempotencyStore(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(10);
    }

    public Task<Guid> GetOrAddAsync(string key, Func<Task<Guid>> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        CleanupExpired();

        var now = DateTimeOffset.UtcNow;
        var entry = _map.AddOrUpdate(
            key,
            _ => new Entry(MakeLazy(factory), now + _ttl),
            (_, existing) => existing.ExpiresAt > now
                ? existing
                : new Entry(MakeLazy(factory), now + _ttl));

        // Tek bir Lazy.Value çağrısı — factory yalnız bir kez çalışır,
        // tüm caller'lar aynı Task'i await eder.
        return entry.Job.Value;
    }

    private static Lazy<Task<Guid>> MakeLazy(Func<Task<Guid>> factory) =>
        new(factory, LazyThreadSafetyMode.ExecutionAndPublication);

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var swept = 0;
        foreach (var kv in _map)
        {
            if (swept >= CleanupBatch) break;
            if (kv.Value.ExpiresAt <= now)
            {
                _map.TryRemove(kv.Key, out _);
                swept++;
            }
        }
    }
}
