using Microsoft.Extensions.Logging.Abstractions;
using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Configuration;
using ThermalPrinterService.Infrastructure.Logging;

namespace ThermalPrinterService.UnitTests;

public sealed class JsonFileLogStoreTests : IDisposable
{
    private readonly string _tempFile;
    private readonly JsonFileLogStore _store;

    public JsonFileLogStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(),
            $"printer-logs-{Guid.NewGuid():N}.json");

        var options = new PrinterOptions { Logs = new LogsOptions { FilePath = _tempFile } };
        _store = new JsonFileLogStore(
            new TestOptionsMonitor<PrinterOptions>(options),
            new TestHostEnvironment(),
            NullLogger<JsonFileLogStore>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public async Task ReadAll_on_missing_file_returns_empty()
    {
        var entries = await _store.ReadAllAsync(default);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task Append_then_read_roundtrips_entry()
    {
        var jobId = Guid.NewGuid();
        await _store.AppendAsync(new LogEntry
        {
            Operation = "print.text",
            Connection = ConnectionMode.Lan,
            JobId = jobId,
            Status = "ok"
        }, default);

        var all = await _store.ReadAllAsync(default);
        Assert.Single(all);
        Assert.Equal("print.text", all[0].Operation);
        Assert.Equal(ConnectionMode.Lan, all[0].Connection);
        Assert.Equal(jobId, all[0].JobId);
        Assert.Equal("ok", all[0].Status);
        Assert.Null(all[0].Error);
    }

    [Fact]
    public async Task Concurrent_appends_do_not_corrupt_file()
    {
        const int writers = 8;
        const int perWriter = 25;

        var tasks = Enumerable.Range(0, writers).Select(w =>
            Task.Run(async () =>
            {
                for (var i = 0; i < perWriter; i++)
                {
                    await _store.AppendAsync(new LogEntry
                    {
                        Operation = $"writer{w}",
                        Status = "ok",
                        JobId = Guid.NewGuid()
                    }, default);
                }
            }));

        await Task.WhenAll(tasks);

        var all = await _store.ReadAllAsync(default);
        Assert.Equal(writers * perWriter, all.Count);

        // Hicbir satir bozulmamali; ReadAll bozuk satirlari atlar, sayim
        // beklenenden az olursa bu yalanlanir.
    }

    [Fact]
    public async Task Append_preserves_chronological_schema_fields()
    {
        await _store.AppendAsync(new LogEntry
        {
            Operation = "connect",
            Connection = ConnectionMode.Usb,
            Status = "ok"
        }, default);

        // null alanlar (jobId, error) JSON cikitisinda yer almamali; ama
        // ReadAll deserialize edince null olarak doner.
        var all = await _store.ReadAllAsync(default);
        Assert.Single(all);
        Assert.Null(all[0].JobId);
        Assert.Null(all[0].Error);
        Assert.Equal(ConnectionMode.Usb, all[0].Connection);

        // Disk uzerindeki ham satir kontrolu: "jobId" anahtari hic gecmemeli.
        var line = (await File.ReadAllTextAsync(_tempFile)).Trim();
        Assert.DoesNotContain("\"jobId\"", line);
        Assert.DoesNotContain("\"error\"", line);
    }
}
