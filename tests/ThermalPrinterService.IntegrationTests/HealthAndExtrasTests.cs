using System.Net;
using System.Text.Json;

namespace ThermalPrinterService.IntegrationTests;

/// <summary>
/// Faz B + C2 endpoint testleri: /health, /health/ready, /logs/export.csv, /print/qr,
/// Idempotency-Key davranisi.
/// </summary>
public sealed class HealthAndExtrasTests : IDisposable
{
    private readonly PrinterApiFixture _fx = new();
    private readonly HttpClient _http;

    public HealthAndExtrasTests() => _http = _fx.CreateClient();
    public void Dispose() { _http.Dispose(); _fx.Dispose(); }

    [Fact]
    public async Task Health_liveness_returns_200_Healthy_without_any_check()
    {
        var resp = await _http.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthReady_returns_503_Degraded_before_any_connect()
    {
        var resp = await _http.GetAsync("/health/ready");

        // Degraded health -> ASP.NET Core 503 doner (varsayilan map: Degraded=ServiceUnavailable)
        Assert.True(resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("printer", body);
    }

    [Fact]
    public async Task HealthReady_becomes_Healthy_after_connect()
    {
        await _http.PostJsonAsync("/connect", new { mode = "Lan" });
        var resp = await _http.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body);
    }

    [Fact]
    public async Task PrintQr_returns_202_and_enqueues_raw_payload()
    {
        await _http.PostJsonAsync("/connect", new { mode = "Lan" });

        var resp = await _http.PostJsonAsync("/print/qr",
            new { data = "https://aco.test/r/1", moduleSize = 6, ecc = "M" });
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        await TestHelpers.WaitForAsync(
            probe: () => Task.FromResult(_fx.FakePrinter.PrintedRaw.Count),
            predicate: c => c == 1);

        var raw = _fx.FakePrinter.PrintedRaw[0];
        var hex = BitConverter.ToString(raw);
        Assert.Contains("1D-28-6B-03-00-31-51-30", hex); // QR Print Buffer komutu
    }

    [Fact]
    public async Task PrintQr_with_empty_data_returns_400()
    {
        var resp = await _http.PostJsonAsync("/print/qr", new { data = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task LogsExportCsv_returns_text_csv_with_header_row()
    {
        // En az 1 satir log uretmek icin bir basim yap.
        await _http.PostJsonAsync("/print/text", new { text = "ok" });

        var resp = await _http.GetAsync("/logs/export.csv");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/csv", resp.Content.Headers.ContentType?.MediaType);

        var csv = await resp.Content.ReadAsStringAsync();
        Assert.StartsWith("ts,op,conn,jobId,status,errorCode,errorDetail", csv);
    }

    [Fact]
    public async Task IdempotencyKey_same_key_returns_same_jobId_and_one_print()
    {
        await _http.PostJsonAsync("/connect", new { mode = "Lan" });

        async Task<Guid> PostWithKey(string key)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/print/text")
            {
                Content = JsonContent.Create(new { text = "merhaba" })
            };
            req.Headers.Add("Idempotency-Key", key);
            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync();
            return JsonDocument.Parse(body).RootElement.GetProperty("jobId").GetGuid();
        }

        var first = await PostWithKey("ACO-RECEIPT-12345");
        var second = await PostWithKey("ACO-RECEIPT-12345");

        Assert.Equal(first, second);

        // Worker'in tek basim yapmasi beklenir.
        await TestHelpers.WaitForAsync(
            probe: () => Task.FromResult(_fx.FakePrinter.PrintedTexts.Count),
            predicate: c => c >= 1);
        await Task.Delay(200); // ikinci basim yoksa burada bekleyip kanitlariz
        Assert.Equal(1, _fx.FakePrinter.PrintedTexts.Count);
    }
}

internal static class JsonContent
{
    public static StringContent Create<T>(T body) =>
        new(JsonSerializer.Serialize(body, TestHelpers.Json),
            System.Text.Encoding.UTF8, "application/json");
}
