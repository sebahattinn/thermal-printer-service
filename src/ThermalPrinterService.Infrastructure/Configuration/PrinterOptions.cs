using System.IO.Ports;

namespace ThermalPrinterService.Infrastructure.Configuration;

/// <summary>
/// Tüm yazıcı ayarları. appsettings.json içindeki "Printer" bölümünden
/// veya environment değişkenlerinden bağlanır (örn. Printer__Lan__Host).
/// Hiçbir varsayılan kritik değer (host, port, COM) burada gömülü değildir.
/// </summary>
public sealed class PrinterOptions
{
    public const string SectionName = "Printer";

    public UsbOptions Usb { get; set; } = new();
    public LanOptions Lan { get; set; } = new();
    public LogsOptions Logs { get; set; } = new();
    public ReconnectOptions Reconnect { get; set; } = new();
    public AuthOptions Auth { get; set; } = new();
    public PaperOptions Paper { get; set; } = new();

    // Tek bir komut için tolere edilecek toplam süre (ms).
    public int CommandTimeoutMs { get; set; } = 5000;

    // Status polling aralığı (ms). 0 ise polling kapalıdır.
    public int StatusPollIntervalMs { get; set; } = 2000;

    // Yazıcı yazdırılabilir genişliği (dot).
    // KP-300: 384, KP-301H: 640, KP-302: 576. Müşteri donanıma göre override etmelidir.
    public int PrintableWidthDots { get; set; } = 384;
}

public sealed class LogsOptions
{
    // Boş bırakılırsa "ContentRoot/logs.json" kullanılır.
    // Override: Printer__Logs__FilePath=/var/log/printer.json
    public string? FilePath { get; set; }

    /// <summary>
    /// Failed job arşivinin yazılacağı klasör. Boşsa "ContentRoot/failed-jobs".
    /// Her başarısız job buraya {jobId}.json olarak yazılır; başarılı reprint'te silinir.
    /// Restart sonrası dosyalar diskten okunup queue'ya geri yüklenir; /reprint çalışmaya devam eder.
    /// </summary>
    public string? FailedJobsDirectory { get; set; }
}

public sealed class ReconnectOptions
{
    public int InitialDelayMs { get; set; } = 1000;
    public int MaxDelayMs { get; set; } = 30000;
    public double Multiplier { get; set; } = 2.0;
    public double JitterFactor { get; set; } = 0.2;
}

public sealed class AuthOptions
{
    /// <summary>
    /// Boş/null ise auth devre dışı. Doluysa her isteğin HeaderName başlığında bu değer
    /// gelmek zorunda. /health, /health/ready, /swagger muaf.
    /// </summary>
    public string? ApiKey { get; set; }

    public string HeaderName { get; set; } = "X-Api-Key";
}

public sealed class PaperOptions
{
    /// <summary>Rulo uzunluğu (metre). KP-300 standart rulo ~80m. Operatör değiştirebilir.</summary>
    public double RollLengthMeters { get; set; } = 80.0;

    /// <summary>Bir satırın yaklaşık yüksekliği (mm). 24-dot font @ 203dpi ≈ 3mm.</summary>
    public double AverageLineHeightMm { get; set; } = 3.0;

    /// <summary>Görsel bir job'un ortalama yüksekliği (mm) — kabaca varsayılan.</summary>
    public double AverageImageHeightMm { get; set; } = 40.0;

    /// <summary>Her job sonunda kesim için harcanan boşluk (mm).</summary>
    public double CutOverheadMm { get; set; } = 5.0;
}

public sealed class UsbOptions
{
    // Örn. "COM3". Boş bırakılırsa USB modu kullanılamaz.
    public string? PortName { get; set; }

    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Handshake Handshake { get; set; } = Handshake.None;

    public int ReadTimeoutMs { get; set; } = 1000;
    public int WriteTimeoutMs { get; set; } = 1000;
}

public sealed class LanOptions
{
    // Örn. "192.168.1.50". Boş bırakılırsa LAN modu kullanılamaz.
    public string? Host { get; set; }

    // ESC/POS uyumlu termal yazıcılar için yaygın port: 9100.
    public int Port { get; set; } = 9100;

    public int ConnectTimeoutMs { get; set; } = 3000;
    public int ReadTimeoutMs { get; set; } = 1000;
    public int WriteTimeoutMs { get; set; } = 1000;
}
