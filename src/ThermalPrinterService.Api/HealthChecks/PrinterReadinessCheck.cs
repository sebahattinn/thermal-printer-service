using Microsoft.Extensions.Diagnostics.HealthChecks;
using ThermalPrinterService.Application.Abstractions;

namespace ThermalPrinterService.Api.HealthChecks;

/// <summary>
/// Readiness probe: yazıcıya aktif bağlantı varsa Healthy, oturum boşsa Degraded,
/// oturumda sürücü var ama bağlı değilse Degraded (servis ayakta, donanım yok).
/// Liveness her zaman ASP.NET Core'un default /health'inden geçer.
/// </summary>
public sealed class PrinterReadinessCheck : IHealthCheck
{
    private readonly IPrinterSession _session;

    public PrinterReadinessCheck(IPrinterSession session) => _session = session;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var p = _session.Current;
        if (p is null)
            return Task.FromResult(HealthCheckResult.Degraded("Hiçbir yazıcı oturumu yok (POST /connect bekleniyor)."));
        if (!p.IsConnected)
            return Task.FromResult(HealthCheckResult.Degraded(
                $"{p.Mode} sürücüsü oturumda, henüz fiziksel bağlantı yok."));

        return Task.FromResult(HealthCheckResult.Healthy(
            $"{p.Mode} bağlı, state={p.CurrentState}."));
    }
}
