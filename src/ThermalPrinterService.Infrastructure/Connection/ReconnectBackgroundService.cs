using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThermalPrinterService.Application.Abstractions;
using ThermalPrinterService.Domain.Entities;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Configuration;

namespace ThermalPrinterService.Infrastructure.Connection;

/// <summary>
/// Tek bir döngüde iki sorumluluk:
///   1) Aktif yazıcı bağlı değilse exponential backoff ile reconnect dener.
///   2) Bağlıysa StatusPollIntervalMs aralıkla cihazı sorgular ve sonucu
///      state machine'e besler.
/// Aktif sürücü /connect ile değişirse (referans değişimi tespit edilir)
/// attempt sayacı sıfırlanır.
/// </summary>
public sealed class ReconnectBackgroundService : BackgroundService
{
    private readonly IPrinterSession _session;
    private readonly IPrinterStateMachine _state;
    private readonly ILogStore _logStore;
    private readonly IOptionsMonitor<PrinterOptions> _options;
    private readonly ILogger<ReconnectBackgroundService> _logger;

    // Hiçbir aktif yazıcı yokken kısa boş döngü uykusu.
    private static readonly TimeSpan _idlePoll = TimeSpan.FromMilliseconds(500);

    public ReconnectBackgroundService(
        IPrinterSession session,
        IPrinterStateMachine state,
        ILogStore logStore,
        IOptionsMonitor<PrinterOptions> options,
        ILogger<ReconnectBackgroundService> logger)
    {
        _session = session;
        _state = state;
        _logStore = logStore;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReconnectBackgroundService başladı.");

        IPrinterService? lastSeen = null;
        var attempt = 0;
        var backoff = BuildBackoff();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var printer = _session.Current;
                if (printer is null)
                {
                    await Task.Delay(_idlePoll, stoppingToken);
                    continue;
                }

                // /connect ile sürücü değişti mi? attempt'i sıfırla.
                if (!ReferenceEquals(printer, lastSeen))
                {
                    lastSeen = printer;
                    attempt = 0;
                    backoff = BuildBackoff();
                }

                if (!printer.IsConnected)
                {
                    attempt++;
                    var delay = backoff.GetDelay(attempt);
                    _logger.LogInformation("Reconnect denemesi #{Attempt} {Delay}ms sonra.",
                        attempt, (int)delay.TotalMilliseconds);
                    await Task.Delay(delay, stoppingToken);

                    var ok = await printer.ConnectAsync(stoppingToken);
                    if (ok)
                    {
                        attempt = 0;
                        if (_state.CanTransitionTo(PrinterState.Ready))
                            _state.TransitionTo(PrinterState.Ready);

                        await _logStore.AppendAsync(new LogEntry
                        {
                            Operation = "reconnect",
                            Connection = printer.Mode,
                            Status = "ok"
                        }, stoppingToken);
                    }
                    else
                    {
                        await _logStore.AppendAsync(new LogEntry
                        {
                            Operation = "reconnect",
                            Connection = printer.Mode,
                            Status = "failed",
                            Error = new LogError(LogErrorCodes.ConnectFailed, $"attempt={attempt}")
                        }, stoppingToken);
                    }
                    continue;
                }

                // Bağlı: status polling.
                var pollMs = _options.CurrentValue.StatusPollIntervalMs;
                if (pollMs <= 0)
                {
                    await Task.Delay(_idlePoll, stoppingToken);
                    continue;
                }

                var observed = await printer.QueryStateAsync(stoppingToken);
                if (_state.CanTransitionTo(observed))
                {
                    _state.TransitionTo(observed);
                }

                await Task.Delay(pollMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Reconnect/polling kendi içinde patladıysa servis durmamalı; loglayıp devam.
                _logger.LogError(ex, "ReconnectBackgroundService döngü hatası.");
                await Task.Delay(_idlePoll, stoppingToken);
            }
        }
    }

    private ExponentialBackoff BuildBackoff()
    {
        var r = _options.CurrentValue.Reconnect;
        return new ExponentialBackoff(
            initial: TimeSpan.FromMilliseconds(r.InitialDelayMs),
            max: TimeSpan.FromMilliseconds(r.MaxDelayMs),
            multiplier: r.Multiplier,
            jitterFactor: r.JitterFactor);
    }
}
