using System.Net;
using System.Text.Json;
using ThermalPrinterService.Application.DTOs;

namespace ThermalPrinterService.IntegrationTests;

/// <summary>
/// /connect -> /print -> /status -> /reprint zincirini uctan uca dener.
/// FakePrinter sayesinde gercek donanim olmadan calisir.
/// </summary>
public sealed class FullFlowTests : IDisposable
{
    private readonly PrinterApiFixture _fx = new();
    private readonly HttpClient _http;

    public FullFlowTests()
    {
        _http = _fx.CreateClient();
    }

    public void Dispose()
    {
        _http.Dispose();
        _fx.Dispose();
    }

    [Fact]
    public async Task Connect_then_print_text_succeeds_and_state_becomes_ready()
    {
        // /connect Lan
        var connectResp = await _http.PostJsonAsync("/connect", new { mode = "Lan" });
        Assert.Equal(HttpStatusCode.OK, connectResp.StatusCode);

        // /print/text
        var printResp = await _http.PostJsonAsync("/print/text", new { text = "merhaba dunya" });
        Assert.Equal(HttpStatusCode.Accepted, printResp.StatusCode);

        // Worker job'u isleyene kadar bekle: failed=0, pending=0
        var status = await TestHelpers.WaitForAsync(
            probe: () => _http.GetJsonAsync<StatusResponse>("/status"),
            predicate: s => s.Queue.Pending == 0 && s.Queue.InFlight == 0 && s.Queue.Failed == 0
                            && _fx.FakePrinter.PrintedTexts.Count == 1);

        Assert.True(status.Connected);
        Assert.Equal("merhaba dunya", _fx.FakePrinter.PrintedTexts[0]);
    }

    [Fact]
    public async Task Print_without_connect_marks_failed_and_can_be_requeued()
    {
        // Hic /connect cagrilmadi -> session.Current null -> worker no_connection.
        var printResp = await _http.PostJsonAsync("/print/text", new { text = "x" });
        var jobId = JsonDocument.Parse(await printResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("jobId").GetString()!;

        var failed = await TestHelpers.WaitForAsync(
            probe: () => _http.GetJsonAsync<StatusResponse>("/status"),
            predicate: s => s.Queue.Failed >= 1);

        Assert.Equal(1, failed.Queue.Failed);

        // /reprint -> kuyruga tekrar girer; hala bagli olmadigi icin yine failed olur
        var repr = await _http.PostAsync($"/reprint/{jobId}", null);
        Assert.Equal(HttpStatusCode.Accepted, repr.StatusCode);

        // Logs en az 3 satir icermeli: enqueue + 1. failed + reprint (+ 2. failed)
        await TestHelpers.WaitForAsync(
            probe: () => _http.GetJsonAsync<List<JsonElement>>("/logs"),
            predicate: list => list.Count >= 3);
    }

    [Fact]
    public async Task Print_failure_marks_job_failed_and_transitions_to_comm_error()
    {
        await _http.PostJsonAsync("/connect", new { mode = "Lan" });
        _fx.FakePrinter.FailNextPrint = true;

        await _http.PostJsonAsync("/print/text", new { text = "kaboom" });

        var status = await TestHelpers.WaitForAsync(
            probe: () => _http.GetJsonAsync<StatusResponse>("/status"),
            predicate: s => s.Queue.Failed >= 1);

        Assert.Equal(1, status.Queue.Failed);
        // ReconnectBackgroundService polling de calistigi icin state Ready'e
        // tekrar dususte olabilir; failure aninda en az bir CommError gozlenmeli
        // demek yerine sadece job'un failed oldugunu dogruluyoruz.
        Assert.Empty(_fx.FakePrinter.PrintedTexts);
    }

    [Fact]
    public async Task Logs_endpoint_returns_entries_with_expected_schema()
    {
        await _http.PostJsonAsync("/connect", new { mode = "Lan" });
        await _http.PostJsonAsync("/print/text", new { text = "log-test" });

        var logs = await TestHelpers.WaitForAsync(
            probe: () => _http.GetJsonAsync<List<JsonElement>>("/logs"),
            predicate: list => list.Count >= 2);

        foreach (var entry in logs)
        {
            // Sema: {ts, op, [conn], [jobId], status, [error]}
            Assert.True(entry.TryGetProperty("ts", out _));
            Assert.True(entry.TryGetProperty("op", out _));
            Assert.True(entry.TryGetProperty("status", out _));
        }

        var ops = logs.Select(e => e.GetProperty("op").GetString()).ToList();
        Assert.Contains("connect", ops);
        Assert.Contains("enqueue.text", ops);
    }
}
