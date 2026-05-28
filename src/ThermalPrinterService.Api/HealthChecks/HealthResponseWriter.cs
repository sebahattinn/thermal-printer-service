using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ThermalPrinterService.Api.HealthChecks;

/// <summary>
/// /health ve /health/ready için JSON yanıt yazıcı.
/// Çıktı: { status, checks: [ { name, status, description } ] }
/// </summary>
internal static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions _opts = new(JsonSerializerDefaults.Web);

    public static Task WriteJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString()
            }).ToArray()
        };

        return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, _opts));
    }
}
