using System.Net;
using System.Text.Json;

namespace ThermalPrinterService.Api.Middleware;

/// <summary>
/// Beklenmedik exception'ları yakalar ve kullanıcı dostu ProblemDetails JSON döner.
/// Stack trace istemciye sızdırılmaz; iç detay yalnızca log'a gider.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // İstek iptali normal akış; loglamaya gerek yok.
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Geçersiz işlem.");
            await WriteProblem(context, HttpStatusCode.Conflict, "invalid_operation", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Beklenmedik hata.");
            await WriteProblem(context, HttpStatusCode.InternalServerError,
                "internal_error", "Sunucuda beklenmedik bir hata oluştu.");
        }
    }

    private static async Task WriteProblem(HttpContext ctx, HttpStatusCode status, string title, string detail)
    {
        ctx.Response.Clear();
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.io/{(int)status}",
            title,
            status = (int)status,
            detail,
            traceId = ctx.TraceIdentifier
        };

        await JsonSerializer.SerializeAsync(ctx.Response.Body, problem, _json);
    }
}
