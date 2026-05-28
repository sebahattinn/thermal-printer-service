namespace ThermalPrinterService.Infrastructure.Connection;

/// <summary>
/// Üs alarak büyüyen, jitterlı bekleme süresi üreticisi.
/// Saf fonksiyon (state tutmaz) — attempt sayısını çağıran taraf izler.
/// Tüm parametreler PrinterOptions.Reconnect'ten gelir; sabit varsayılan yok.
/// </summary>
public sealed class ExponentialBackoff
{
    public TimeSpan Initial { get; init; }
    public TimeSpan Max { get; init; }
    public double Multiplier { get; init; }
    public double JitterFactor { get; init; }

    public ExponentialBackoff(TimeSpan initial, TimeSpan max, double multiplier, double jitterFactor)
    {
        if (initial <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initial));
        if (max < initial) throw new ArgumentOutOfRangeException(nameof(max));
        if (multiplier < 1.0) throw new ArgumentOutOfRangeException(nameof(multiplier));
        if (jitterFactor < 0 || jitterFactor > 1) throw new ArgumentOutOfRangeException(nameof(jitterFactor));

        Initial = initial;
        Max = max;
        Multiplier = multiplier;
        JitterFactor = jitterFactor;
    }

    /// <summary>
    /// attempt 1'den başlamalı; 0 verilirse Initial döner.
    /// </summary>
    public TimeSpan GetDelay(int attempt)
    {
        if (attempt <= 0) return Initial;

        // 2^(attempt-1) yerine genel Multiplier üzerinden.
        var raw = Initial.TotalMilliseconds * Math.Pow(Multiplier, attempt - 1);
        var capped = Math.Min(raw, Max.TotalMilliseconds);

        // Jitter: +/- (capped * JitterFactor) aralığında.
        var jitterRange = capped * JitterFactor;
        var jitter = (Random.Shared.NextDouble() * 2 - 1) * jitterRange;
        var withJitter = Math.Max(0, capped + jitter);

        return TimeSpan.FromMilliseconds(withJitter);
    }
}
