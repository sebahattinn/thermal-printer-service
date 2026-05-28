using System.Net;
using System.Text.Json;

namespace ThermalPrinterService.IntegrationTests;

/// <summary>
/// Endpoint sozlesme dogrulamalari: HTTP statuleri, validation, ProblemDetails.
/// Her test izole bir WebApplicationFactory kullanir — paylasilan kuyruk veya
/// arka plan servis durumu yarismaya yol acmasin diye.
/// </summary>
public sealed class EndpointValidationTests : IDisposable
{
    private readonly PrinterApiFixture _fx = new();
    private readonly HttpClient _http;

    public EndpointValidationTests()
    {
        _http = _fx.CreateClient();
    }

    public void Dispose()
    {
        _http.Dispose();
        _fx.Dispose();
    }

    [Fact]
    public async Task Status_initial_returns_disconnected_and_empty_queue()
    {
        var json = await _http.GetStringAsync("/status");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("connected").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("mode").ValueKind);
        Assert.Equal("Disconnected", root.GetProperty("state").GetString());
        Assert.Equal(0, root.GetProperty("queue").GetProperty("pending").GetInt32());
    }

    [Fact]
    public async Task PrintImage_with_invalid_base64_returns_400()
    {
        var resp = await _http.PostJsonAsync("/print/image",
            new { imageBase64 = "this-is-not-base64!@#$" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("invalid image", body);
    }

    [Fact]
    public async Task PrintText_with_empty_body_returns_400()
    {
        var resp = await _http.PostJsonAsync("/print/text", new { text = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Reprint_unknown_id_returns_404()
    {
        var resp = await _http.PostAsync($"/reprint/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Connect_with_invalid_mode_returns_400()
    {
        var resp = await _http.PostJsonAsync("/connect", new { mode = "Bluetooth" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PrintText_returns_202_with_jobId()
    {
        var resp = await _http.PostJsonAsync("/print/text", new { text = "hi" });
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("jobId", out var jobIdProp));
        Assert.True(Guid.TryParse(jobIdProp.GetString(), out _));
        Assert.Equal("queued", doc.RootElement.GetProperty("status").GetString());
    }
}
