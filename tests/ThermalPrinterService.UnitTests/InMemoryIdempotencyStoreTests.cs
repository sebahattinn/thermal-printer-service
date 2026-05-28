using ThermalPrinterService.Infrastructure.Queueing;

namespace ThermalPrinterService.UnitTests;

public sealed class InMemoryIdempotencyStoreTests
{
    [Fact]
    public async Task GetOrAdd_returns_same_jobId_for_same_key()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(5));
        var first = await store.GetOrAddAsync("k1", () => Task.FromResult(Guid.NewGuid()), default);
        var second = await store.GetOrAddAsync("k1", () => Task.FromResult(Guid.NewGuid()), default);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetOrAdd_returns_different_jobId_for_different_key()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(5));
        var a = await store.GetOrAddAsync("k1", () => Task.FromResult(Guid.NewGuid()), default);
        var b = await store.GetOrAddAsync("k2", () => Task.FromResult(Guid.NewGuid()), default);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task GetOrAdd_calls_factory_again_after_ttl_expires()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMilliseconds(50));
        var first = await store.GetOrAddAsync("k1", () => Task.FromResult(Guid.NewGuid()), default);
        await Task.Delay(120);
        var second = await store.GetOrAddAsync("k1", () => Task.FromResult(Guid.NewGuid()), default);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Concurrent_calls_with_same_key_resolve_to_one_jobId()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(5));
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => store.GetOrAddAsync("k1", () => Task.FromResult(Guid.NewGuid()), default))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Hepsi aynı jobId olmalı (yarış olsa bile)
        Assert.Single(results.Distinct());
    }
}
