using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Configuration;

namespace ThermalPrinterService.Infrastructure.Logging;

/// <summary>
/// Failed job'ları diskte tek-JSON-dosya-per-job şeklinde saklar.
/// </summary>
public sealed class FileSystemFailedJobArchive : IFailedJobArchive
{
    private readonly IOptionsMonitor<PrinterOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<FileSystemFailedJobArchive> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileSystemFailedJobArchive(
        IOptionsMonitor<PrinterOptions> options,
        IHostEnvironment env,
        ILogger<FileSystemFailedJobArchive> logger)
    {
        _options = options;
        _env = env;
        _logger = logger;
    }

    public async Task SaveAsync(PrintJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var dir = ResolveDirectory();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(dir);
            var dto = ArchivedJob.FromDomain(job);
            var path = Path.Combine(dir, $"{job.Id}.json");

            var tmp = path + ".tmp";
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None,
                              bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(fs, dto, _json, cancellationToken);
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed job arşivi yazılamadı: {JobId}", job.Id);
        }
        finally { _lock.Release(); }
    }

    public Task DeleteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(ResolveDirectory(), $"{jobId}.json");
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed job arşivi silinemedi: {JobId}", jobId);
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PrintJob>> LoadAllAsync(CancellationToken cancellationToken)
    {
        var dir = ResolveDirectory();
        if (!Directory.Exists(dir)) return Array.Empty<PrintJob>();

        var result = new List<PrintJob>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                await using var fs = File.OpenRead(file);
                var dto = await JsonSerializer.DeserializeAsync<ArchivedJob>(fs, _json, cancellationToken);
                if (dto is not null) result.Add(dto.ToDomain());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bozuk failed job dosyası atlandı: {Path}", file);
            }
        }
        return result;
    }

    private string ResolveDirectory()
    {
        var configured = _options.CurrentValue.Logs.FailedJobsDirectory;
        if (string.IsNullOrWhiteSpace(configured)) configured = "failed-jobs";
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_env.ContentRootPath, configured);
    }

    private sealed record ArchivedJob(
        Guid Id,
        JobKind Kind,
        string? Text,
        string? ImageBytesBase64,
        string? RawBytesBase64,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        int Attempts,
        string? LastError)
    {
        public static ArchivedJob FromDomain(PrintJob j) => new(
            j.Id, j.Kind, j.Text,
            j.ImageBytes is null ? null : Convert.ToBase64String(j.ImageBytes),
            j.RawBytes is null ? null : Convert.ToBase64String(j.RawBytes),
            j.CreatedAt, j.CompletedAt, j.Attempts, j.LastError);

        public PrintJob ToDomain() => new()
        {
            Id = Id,
            Kind = Kind,
            Text = Text,
            ImageBytes = ImageBytesBase64 is null ? null : Convert.FromBase64String(ImageBytesBase64),
            RawBytes = RawBytesBase64 is null ? null : Convert.FromBase64String(RawBytesBase64),
            CreatedAt = CreatedAt,
            CompletedAt = CompletedAt,
            Attempts = Attempts,
            LastError = LastError,
            Status = JobStatus.Failed
        };
    }
}
