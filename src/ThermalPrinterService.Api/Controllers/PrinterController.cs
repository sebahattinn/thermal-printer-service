using Microsoft.AspNetCore.Mvc;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Application.DTOs;
using ThermalPrinterService.Application.Receipts;
using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Api.Controllers;

[ApiController]
[Route("")]
public sealed class PrinterController : ControllerBase
{
    private readonly IPrinterFactory _factory;
    private readonly IPrinterSession _session;
    private readonly IPrintQueue _queue;
    private readonly ILogStore _logs;
    private readonly IPrinterStateMachine _state;
    private readonly IReceiptComposer _composer;
    private readonly IIdempotencyStore _idempotency;
    private readonly IPaperUsageTracker _paper;
    private readonly ILogger<PrinterController> _logger;

    private const string IdempotencyHeader = "Idempotency-Key";

    public PrinterController(
        IPrinterFactory factory,
        IPrinterSession session,
        IPrintQueue queue,
        ILogStore logs,
        IPrinterStateMachine state,
        IReceiptComposer composer,
        IIdempotencyStore idempotency,
        IPaperUsageTracker paper,
        ILogger<PrinterController> logger)
    {
        _factory = factory;
        _session = session;
        _queue = queue;
        _logs = logs;
        _state = state;
        _composer = composer;
        _idempotency = idempotency;
        _paper = paper;
        _logger = logger;
    }

    /// <summary>
    /// Idempotency-Key header varsa store üzerinden çözümler; yoksa direkt factory çağırır.
    /// </summary>
    private Task<Guid> EnqueueIdempotentAsync(Func<Task<Guid>> factory, CancellationToken ct)
    {
        if (Request.Headers.TryGetValue(IdempotencyHeader, out var key) && !string.IsNullOrWhiteSpace(key))
            return _idempotency.GetOrAddAsync(key.ToString(), factory, ct);
        return factory();
    }

    // POST /connect
    // Body: { "mode": "Usb" } veya { "mode": "Lan" }
    // Aktif sürücüyü değiştirir; reconnect/backoff arka plan servisi
    // (ReconnectBackgroundService) IPrinterSession.Current'i izleyerek devreye girer.
    [HttpPost("connect")]
    [ProducesResponseType(typeof(ConnectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var printer = _factory.Create(request.Mode);
        var ok = await printer.ConnectAsync(ct);
        _session.Set(printer);

        await _logs.AppendAsync(new LogEntry
        {
            Operation = "connect",
            Connection = request.Mode,
            Status = ok ? "ok" : "pending",
            Error = ok ? null : new LogError(LogErrorCodes.ConnectFailed, "initial connect failed; reconnect scheduled")
        }, ct);

        return Ok(new ConnectResponse
        {
            Connected = ok,
            Mode = request.Mode,
            Message = ok ? "connected" : "initial connect failed; background reconnect active"
        });
    }

    // POST /print/text
    [HttpPost("print/text")]
    [ProducesResponseType(typeof(EnqueuedResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> PrintText([FromBody] PrintTextRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var job = new PrintJob { Kind = JobKind.Text, Text = request.Text };
        var jobId = await EnqueueIdempotentAsync(() => _queue.EnqueueAsync(job, ct), ct);

        await _logs.AppendAsync(new LogEntry
        {
            Operation = "enqueue.text",
            Connection = _session.Current?.Mode,
            JobId = jobId,
            Status = "queued"
        }, ct);

        return Accepted(new EnqueuedResponse { JobId = jobId });
    }

    // POST /print/image
    [HttpPost("print/image")]
    [ProducesResponseType(typeof(EnqueuedResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> PrintImage([FromBody] PrintImageRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.ImageBase64);
        }
        catch (FormatException)
        {
            return Problem(
                title: "invalid image",
                detail: "ImageBase64 alanı geçerli bir base64 değeri olmalıdır.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var job = new PrintJob { Kind = JobKind.Image, ImageBytes = bytes };
        var jobId = await EnqueueIdempotentAsync(() => _queue.EnqueueAsync(job, ct), ct);

        await _logs.AppendAsync(new LogEntry
        {
            Operation = "enqueue.image",
            Connection = _session.Current?.Mode,
            JobId = jobId,
            Status = "queued"
        }, ct);

        return Accepted(new EnqueuedResponse { JobId = jobId });
    }

    // GET /status
    [HttpGet("status")]
    [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
    public IActionResult Status()
    {
        var printer = _session.Current;
        var last = _queue.GetLastCompleted();

        var response = new StatusResponse
        {
            Connected = printer?.IsConnected ?? false,
            Mode = printer?.Mode,
            State = _state.Current,
            Queue = _queue.GetSummary(),
            LastJob = last is null ? null : new LastJobInfo
            {
                JobId = last.Id,
                Kind = last.Kind.ToString(),
                Status = last.Status,
                CompletedAt = last.CompletedAt,
                Attempts = last.Attempts,
                ErrorCode = last.LastError
            },
            Paper = _paper.GetInfo()
        };
        return Ok(response);
    }

    // GET /paper
    // Yalnız kağıt bilgisi (UI'de paper card için kullanılır).
    [HttpGet("paper")]
    [ProducesResponseType(typeof(PaperInfo), StatusCodes.Status200OK)]
    public IActionResult Paper() => Ok(_paper.GetInfo());

    // POST /paper/reset
    // Operatör yeni rulo taktı; sayaç sıfırlanır.
    [HttpPost("paper/reset")]
    [ProducesResponseType(typeof(PaperInfo), StatusCodes.Status200OK)]
    public async Task<IActionResult> PaperReset(CancellationToken ct)
    {
        _paper.ResetRoll();
        await _logs.AppendAsync(new LogEntry
        {
            Operation = "paper.reset",
            Status = "ok"
        }, ct);
        return Ok(_paper.GetInfo());
    }

    // GET /logs
    [HttpGet("logs")]
    [ProducesResponseType(typeof(IReadOnlyList<LogEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logs(CancellationToken ct)
    {
        var all = await _logs.ReadAllAsync(ct);
        return Ok(all);
    }

    // GET /logs/export.csv
    // RFC 4180 uyumlu CSV; tarayıcıdan tıklamayla indirilir, Excel/Sheets açabilir.
    [HttpGet("logs/export.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> LogsCsv(CancellationToken ct)
    {
        var all = await _logs.ReadAllAsync(ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ts,op,conn,jobId,status,errorCode,errorDetail");

        foreach (var e in all)
        {
            sb.Append(e.Timestamp.ToString("O")).Append(',');
            sb.Append(CsvField(e.Operation)).Append(',');
            sb.Append(e.Connection?.ToString() ?? "").Append(',');
            sb.Append(e.JobId?.ToString() ?? "").Append(',');
            sb.Append(CsvField(e.Status)).Append(',');
            sb.Append(CsvField(e.Error?.Code ?? "")).Append(',');
            sb.AppendLine(CsvField(e.Error?.Detail ?? ""));
        }

        var fileName = $"printer-logs-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    private static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // RFC 4180: comma, quote, newline iceriyorsa cift tirnak icine al, ic tirnaklari escape et
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    // POST /print/qr
    // Body: { "data": "https://...", "moduleSize": 6, "ecc": "M", "alignment": "Center" }
    // Composer ile tek QR + cut kompoze edilir; kuyruğa Raw olarak atılır.
    [HttpPost("print/qr")]
    [ProducesResponseType(typeof(EnqueuedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PrintQr([FromBody] PrintQrRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var doc = new ReceiptBuilder()
            .Qr(request.Data, request.ModuleSize, request.Ecc, request.Alignment)
            .Feed(3)
            .Cut()
            .Build();

        byte[] payload;
        try { payload = _composer.Compose(doc); }
        catch (Exception ex)
        {
            return Problem(title: "compose_failed", detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var job = new PrintJob { Kind = JobKind.Raw, RawBytes = payload };
        var jobId = await EnqueueIdempotentAsync(() => _queue.EnqueueAsync(job, ct), ct);

        await _logs.AppendAsync(new LogEntry
        {
            Operation = "enqueue.qr",
            Connection = _session.Current?.Mode,
            JobId = jobId,
            Status = "queued"
        }, ct);

        return Accepted(new EnqueuedResponse { JobId = jobId });
    }

    // POST /print/receipt
    // Body: { "elements": [ { "type":"text","text":"...", ... }, ... ] }
    // Receipt elementlerini ESC/POS byte zincirine kompoze eder ve kuyruğa atar.
    [HttpPost("print/receipt")]
    [ProducesResponseType(typeof(EnqueuedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PrintReceipt([FromBody] PrintReceiptRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        ReceiptDocument doc;
        try
        {
            doc = BuildDocument(request);
        }
        catch (FormatException ex)
        {
            return Problem(
                title: "invalid receipt element",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        byte[] payload;
        try
        {
            payload = _composer.Compose(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt compose hatası.");
            return Problem(
                title: "compose_failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var job = new PrintJob { Kind = JobKind.Raw, RawBytes = payload };
        var jobId = await EnqueueIdempotentAsync(() => _queue.EnqueueAsync(job, ct), ct);

        await _logs.AppendAsync(new LogEntry
        {
            Operation = "enqueue.receipt",
            Connection = _session.Current?.Mode,
            JobId = jobId,
            Status = "queued"
        }, ct);

        return Accepted(new EnqueuedResponse { JobId = jobId });
    }

    private static ReceiptDocument BuildDocument(PrintReceiptRequest request)
    {
        var builder = new ReceiptBuilder();
        foreach (var e in request.Elements)
        {
            switch (e.Type.ToLowerInvariant())
            {
                case "text":
                    builder.Text(e.Text ?? string.Empty, e.Alignment, e.Bold, e.Width, e.Height, e.Underline);
                    break;
                case "image":
                    if (string.IsNullOrWhiteSpace(e.ImageBase64))
                        throw new FormatException("image elementi ImageBase64 zorunlu.");
                    byte[] img;
                    try { img = Convert.FromBase64String(e.ImageBase64); }
                    catch (FormatException) { throw new FormatException("ImageBase64 geçerli base64 olmalı."); }
                    builder.Image(img, e.Alignment);
                    break;
                case "qr":
                    if (string.IsNullOrEmpty(e.QrData))
                        throw new FormatException("qr elementi QrData zorunlu.");
                    builder.Qr(e.QrData, e.QrModuleSize, e.QrEcc, e.Alignment);
                    break;
                case "table":
                    if (e.Headers is null || e.Headers.Count == 0)
                        throw new FormatException("table elementi Headers zorunlu.");
                    var rows = (e.Rows ?? new()).Select(r => (IReadOnlyList<string>)r).ToList();
                    builder.Table(e.Headers, rows, e.HeaderBold);
                    break;
                case "feed":
                    builder.Feed(e.Lines);
                    break;
                case "separator":
                    var ch = string.IsNullOrEmpty(e.Character) ? '-' : e.Character[0];
                    builder.Separator(ch);
                    break;
                case "cut":
                    builder.Cut(e.FeedDots);
                    break;
                default:
                    throw new FormatException($"Bilinmeyen receipt element type: {e.Type}");
            }
        }
        return builder.Build();
    }

    // POST /reprint/{jobId}
    [HttpPost("reprint/{jobId:guid}")]
    [ProducesResponseType(typeof(EnqueuedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reprint(Guid jobId, CancellationToken ct)
    {
        var ok = await _queue.RequeueAsync(jobId, ct);
        if (!ok)
        {
            return Problem(
                title: "job not found",
                detail: $"jobId={jobId} bulunamadı veya yeniden basılamaz.",
                statusCode: StatusCodes.Status404NotFound);
        }

        await _logs.AppendAsync(new LogEntry
        {
            Operation = "reprint",
            Connection = _session.Current?.Mode,
            JobId = jobId,
            Status = "queued"
        }, ct);

        return Accepted(new EnqueuedResponse { JobId = jobId, Status = "requeued" });
    }
}
