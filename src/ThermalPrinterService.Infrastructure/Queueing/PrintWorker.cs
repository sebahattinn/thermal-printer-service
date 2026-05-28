using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Infrastructure.Queueing;

/// <summary>
/// Kuyruktan tek tek job'ları çekip aktif yazıcıya gönderen tüketici.
/// Bir job için karar matrisi:
///   - Aktif yazıcı yoksa veya bağlı değilse  -> Failed (kullanıcı /reprint ile tetikleyebilir)
///   - Yazma başarılıysa                      -> Succeeded + Ready'e geçiş
///   - Yazma exception fırlatırsa             -> Failed + CommError'a geçiş
/// </summary>
public sealed class PrintWorker : BackgroundService
{
    private readonly IPrintQueue _queue;
    private readonly IPrinterSession _session;
    private readonly IPrinterStateMachine _state;
    private readonly ILogStore _logStore;
    private readonly IPaperUsageTracker _paper;
    private readonly IFailedJobArchive _archive;
    private readonly ILogger<PrintWorker> _logger;

    public PrintWorker(
        IPrintQueue queue,
        IPrinterSession session,
        IPrinterStateMachine state,
        ILogStore logStore,
        IPaperUsageTracker paper,
        IFailedJobArchive archive,
        ILogger<PrintWorker> logger)
    {
        _queue = queue;
        _session = session;
        _state = state;
        _logStore = logStore;
        _paper = paper;
        _archive = archive;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PrintWorker başladı.");
        try
        {
            await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
            {
                await ProcessAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PrintWorker beklenmedik şekilde durdu.");
        }
    }

    private static string OperationName(JobKind kind) => kind switch
    {
        JobKind.Text => "print.text",
        JobKind.Image => "print.image",
        JobKind.Raw => "print.receipt",
        _ => "print.unknown"
    };

    private async Task ProcessAsync(PrintJob job, CancellationToken ct)
    {
        job.Status = JobStatus.Printing;
        job.Attempts++;

        var printer = _session.Current;
        if (printer is null || !printer.IsConnected)
        {
            job.Status = JobStatus.Failed;
            job.LastError = LogErrorCodes.NoConnection;
            job.CompletedAt = DateTimeOffset.UtcNow;
            _queue.MarkCompleted(job);
            await _archive.SaveAsync(job, ct);
            await _logStore.AppendAsync(new LogEntry
            {
                Operation = OperationName(job.Kind),
                Connection = printer?.Mode,
                JobId = job.Id,
                Status = "failed",
                Error = new LogError(LogErrorCodes.NoConnection,
                    "Yazıcı bağlı değil; /connect ile bağlanın veya reconnect başarılı olunca yeniden basın.")
            }, ct);
            return;
        }

        var op = OperationName(job.Kind);
        try
        {
            switch (job.Kind)
            {
                case JobKind.Text:
                    await printer.PrintTextAsync(job.Text ?? string.Empty, ct);
                    break;
                case JobKind.Image:
                    await printer.PrintImageAsync(job.ImageBytes ?? ReadOnlyMemory<byte>.Empty, ct);
                    break;
                case JobKind.Raw:
                    // Raw payload zaten init+...+cut içeren tam ESC/POS zinciri;
                    // PrintRawAsync sarmadan birebir gönderir.
                    await printer.PrintRawAsync(job.RawBytes ?? ReadOnlyMemory<byte>.Empty, ct);
                    break;
            }

            job.Status = JobStatus.Succeeded;
            job.CompletedAt = DateTimeOffset.UtcNow;
            _queue.MarkCompleted(job);
            _paper.RegisterPrinted(job);
            await _archive.DeleteAsync(job.Id, ct);

            // Başarılı yazımdan sonra state machine'i Ready'e çek; uygun geçiş değilse atla.
            if (_state.CanTransitionTo(PrinterState.Ready))
                _state.TransitionTo(PrinterState.Ready);

            await _logStore.AppendAsync(new LogEntry
            {
                Operation = op,
                Connection = printer.Mode,
                JobId = job.Id,
                Status = "ok"
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.LastError = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            _queue.MarkCompleted(job);
            await _archive.SaveAsync(job, ct);

            if (_state.CanTransitionTo(PrinterState.CommError))
                _state.TransitionTo(PrinterState.CommError);

            // Eger state machine sayesinde bilinen bir donanim durumu varsa onu kullan;
            // yoksa generic PrintFailed.
            var code = _state.Current == PrinterState.Ready
                ? LogErrorCodes.PrintFailed
                : LogErrorCodes.FromPrinterState(_state.Current);

            await _logStore.AppendAsync(new LogEntry
            {
                Operation = op,
                Connection = printer.Mode,
                JobId = job.Id,
                Status = "failed",
                Error = new LogError(code, ex.Message)
            }, ct);
        }
    }
}
