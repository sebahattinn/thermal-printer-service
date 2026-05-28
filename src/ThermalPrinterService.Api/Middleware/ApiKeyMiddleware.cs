using System.Text.Json;
using Microsoft.Extensions.Options;
using ThermalPrinterService.Infrastructure.Configuration;

namespace ThermalPrinterService.Api.Middleware;

/// <summary>
/// Basit token bazlı yetkilendirme. PrinterOptions.Auth.ApiKey null/boş ise
/// middleware tamamen no-op (yerel geliştirme rahat olsun). Doluysa her istek
/// HeaderName başlığında bu değeri taşımak zorunda; aksi halde 401 döner.
/// Muafiyetler: /health, /health/ready, /swagger*, /_blazor*, statik kaynaklar.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<PrinterOptions> _options;

    private static readonly string[] _exemptPrefixes = new[]
    {
        "/health", "/swagger", "/_blazor", "/_framework", "/css", "/js", "/lib", "/favicon"
    };

    public ApiKeyMiddleware(RequestDelegate next, IOptionsMonitor<PrinterOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var auth = _options.CurrentValue.Auth;
        if (string.IsNullOrWhiteSpace(auth.ApiKey))
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? string.Empty;
        foreach (var p in _exemptPrefixes)
        {
            if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                await _next(ctx);
                return;
            }
        }

        if (!ctx.Request.Headers.TryGetValue(auth.HeaderName, out var provided)
            || !string.Equals(provided.ToString(), auth.ApiKey, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/problem+json";
            var problem = new
            {
                type = "https://httpstatuses.io/401",
                title = "unauthorized",
                status = 401,
                detail = $"Geçerli bir {auth.HeaderName} başlığı zorunlu."
            };
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            return;
        }

        await _next(ctx);
    }
}
