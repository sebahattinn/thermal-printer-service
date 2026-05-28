using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Infrastructure.Configuration;

namespace ThermalPrinterService.Infrastructure.Logging;

/// <summary>
/// Disk üzerinde JSONL (JSON-per-line) olarak tutulan log deposu.
/// Bu format atomic append'i ucuz yapar: tek bir satır eklemek dosyayı
/// yeniden serileştirmek gerektirmez ve okurken kısmi yazımlardan etkilenmez.
/// API tarafına döndüğümüzde tek seferde JSON array olarak deserialize edilir.
///
/// Yol IOptionsMonitor üzerinden config-driven; hiçbir sabit yol yok.
/// </summary>
public sealed class JsonFileLogStore : ILogStore
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly IOptionsMonitor<PrinterOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<JsonFileLogStore> _logger;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonFileLogStore(
        IOptionsMonitor<PrinterOptions> options,
        IHostEnvironment env,
        ILogger<JsonFileLogStore> logger)
    {
        _options = options;
        _env = env;
        _logger = logger;
    }

    public async Task AppendAsync(LogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var path = ResolvePath();

        var line = JsonSerializer.Serialize(entry, _json) + Environment.NewLine;
        var bytes = Encoding.UTF8.GetBytes(line);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectory(path);

            // FileShare.Read: yazarken /logs okuması çakışmasın.
            await using var fs = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            await fs.WriteAsync(bytes, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log dosyasına yazamamak servisi düşürmez; yalnızca uyarı.
            _logger.LogWarning(ex, "Log satırı yazılamadı: {Path}", path);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<LogEntry>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath();
        if (!File.Exists(path)) return Array.Empty<LogEntry>();

        var result = new List<LogEntry>();

        // FileShare.ReadWrite: append devam ederken okuyabilelim.
        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(fs, Encoding.UTF8);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var entry = JsonSerializer.Deserialize<LogEntry>(line, _json);
                if (entry is not null) result.Add(entry);
            }
            catch (JsonException ex)
            {
                // Bozuk satır varsa atla; üretim ortamında nadir görülür ama
                // yarı-yazılmış bir satırla karşılaşırsak crash etmek istemiyoruz.
                _logger.LogWarning(ex, "Bozuk log satırı atlandı.");
            }
        }

        return result;
    }

    private string ResolvePath()
    {
        var configured = _options.CurrentValue.Logs.FilePath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            // Config boşsa content root altında varsayılan dosya.
            configured = "logs.json";
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_env.ContentRootPath, configured);
    }

    private static void EnsureDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
