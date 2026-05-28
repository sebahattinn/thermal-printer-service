using System.Net;
using System.Text.Json;

namespace ThermalPrinterService.IntegrationTests;

/// <summary>
/// Brief 3. madde: "Basılmayan görseller kaydedilmeli ve UI'da Tekrar Bastır seçeneği olmalı."
/// Bu test diskte kaydolma + restart simulation + reprint zincirinin tamamını kapsıyor.
/// </summary>
public sealed class FailedJobArchiveTests : IDisposable
{
    private PrinterApiFixture _fx = new();
    private HttpClient _http;

    public FailedJobArchiveTests() => _http = _fx.CreateClient();

    public void Dispose()
    {
        _http.Dispose();
        _fx.Dispose();
    }

    [Fact]
    public async Task Failed_job_is_written_to_disk_archive()
    {
        // Bağlantı yok -> job NoConnection ile failed olur ve diske yazılır.
        var resp = await _http.PostJsonAsync("/print/text", new { text = "persist-to-disk-payload" });
        var jobId = JsonDocument.Parse(await resp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("jobId").GetString()!;

        // Worker'in failed işaretlemesini bekle
        await TestHelpers.WaitForAsync(
            probe: () => Task.FromResult(File.Exists(Path.Combine(_fx.FailedJobsDir, $"{jobId}.json"))),
            predicate: exists => exists);

        var path = Path.Combine(_fx.FailedJobsDir, $"{jobId}.json");
        Assert.True(File.Exists(path));

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("persist-to-disk-payload", json);
        Assert.Contains(jobId, json);
        Assert.Contains("NO_CONNECTION", json);
    }

    [Fact]
    public async Task Successful_reprint_deletes_archive_file()
    {
        // Önce bağlantısız ortamda fail ettir, sonra connect+reprint ile success yap.
        var resp1 = await _http.PostJsonAsync("/print/text", new { text = "reprint testi" });
        var jobId = JsonDocument.Parse(await resp1.Content.ReadAsStringAsync())
            .RootElement.GetProperty("jobId").GetString()!;

        var path = Path.Combine(_fx.FailedJobsDir, $"{jobId}.json");
        await TestHelpers.WaitForAsync(
            probe: () => Task.FromResult(File.Exists(path)),
            predicate: exists => exists);

        // Bağlan, reprint et — başarılı olunca dosya silinmeli.
        await _http.PostJsonAsync("/connect", new { mode = "Lan" });
        await _http.PostAsync($"/reprint/{jobId}", null);

        await TestHelpers.WaitForAsync(
            probe: () => Task.FromResult(!File.Exists(path)),
            predicate: deleted => deleted);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Restart_rehydrates_failed_jobs_from_disk_and_reprint_works()
    {
        // 1. faz: failed job üret, diske yazılsın.
        var resp = await _http.PostJsonAsync("/print/text", new { text = "restart-survival" });
        var jobId = JsonDocument.Parse(await resp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("jobId").GetString()!;

        var path = Path.Combine(_fx.FailedJobsDir, $"{jobId}.json");
        await TestHelpers.WaitForAsync(
            probe: () => Task.FromResult(File.Exists(path)),
            predicate: exists => exists);

        // 2. faz: servisi "restart" et — host'u dispose et, yeni host kur, AYNI diski kullan.
        var oldFx = _fx;
        var oldHttp = _http;

        _fx = new PrinterApiFixture(); // yeni fixture, FARKLI klasör default
        // Aynı diski kullansın diye reflection yerine: yeni fixture'in failed-jobs dir'ini eski'sine eşitle.
        // PrinterApiFixture FailedJobsDir init'i ctor'da, override yolu yok.
        // Bu test için: aynı klasörü etkilemek üzere fixture'ı manuel kontrol edilmiş şekilde kuralım.
        // Pratik çözüm: oldFx'in dir'ini al, dosyayı yeni fixture'in dir'ine kopyala.
        Directory.CreateDirectory(_fx.FailedJobsDir);
        File.Copy(path, Path.Combine(_fx.FailedJobsDir, $"{jobId}.json"), overwrite: true);
        _http = _fx.CreateClient();

        oldHttp.Dispose();
        oldFx.Dispose();

        // 3. faz: yeni host bağlan + /reprint çağır. RehydrationService dict'i doldurmuş olmalı,
        // /reprint jobId'yi bulup tekrar kuyruğa atmalı.
        await _http.PostJsonAsync("/connect", new { mode = "Lan" });
        var reprintResp = await _http.PostAsync($"/reprint/{jobId}", null);

        Assert.Equal(HttpStatusCode.Accepted, reprintResp.StatusCode);
    }
}
