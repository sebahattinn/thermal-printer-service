using System.Text;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Infrastructure.Printers;

/// <summary>
/// ESC/POS komut bayt üreticisi (Cashino KP-300/KP-301H/KP-302 uyumlu).
/// USB ve LAN sürücüleri tek bu sınıftan faydalanır; komut bilgisi tek noktada yaşar.
/// Yeni format/komut eklemek istersen sadece burada bir metot/sabit ekle.
/// </summary>
public static class EscPosCommands
{
    // ----- Temel kontrol baytları -----

    // ESC @  ->  yazıcıyı sıfırla / init
    public static ReadOnlySpan<byte> Init => new byte[] { 0x1B, 0x40 };

    // LF  ->  bir satır atla
    public static ReadOnlySpan<byte> LineFeed => new byte[] { 0x0A };

    // CR  ->  satır başına dön
    public static ReadOnlySpan<byte> CarriageReturn => new byte[] { 0x0D };

    // GS V 0  ->  kağıdı tam keser
    public static ReadOnlySpan<byte> CutPaper => new byte[] { 0x1D, 0x56, 0x00 };

    // GS V 1  ->  kağıdı kısmi keser
    public static ReadOnlySpan<byte> PartialCut => new byte[] { 0x1D, 0x56, 0x01 };

    // ----- 4 ayrı status sorgusu (DLE EOT n) -----
    // KP-300 manuel sayfa 67: real-time, sadece serial portta çalışır
    // ama TCP üzerinden de yazıcı yanıt veriyor (test edildi).

    // DLE EOT 1  ->  printer status (drawer, on/offline, paper torn)
    public static ReadOnlySpan<byte> StatusRequestPrinter
        => new byte[] { 0x10, 0x04, 0x01 };

    // DLE EOT 2  ->  offline status (cover, paper shortage, error)
    public static ReadOnlySpan<byte> StatusRequestOffline
        => new byte[] { 0x10, 0x04, 0x02 };

    // DLE EOT 3  ->  error status (cutter, overheat, unrecoverable)
    public static ReadOnlySpan<byte> StatusRequestError
        => new byte[] { 0x10, 0x04, 0x03 };

    // DLE EOT 4  ->  paper sensor (paper end, near end)
    public static ReadOnlySpan<byte> StatusRequestPaperSensor
        => new byte[] { 0x10, 0x04, 0x04 };

    // ----- Formatlama komutları (parametreli, byte[] döner) -----

    public enum Alignment : byte { Left = 0, Center = 1, Right = 2 }

    /// <summary>ESC a n  ->  hizalama (sola/orta/sağa).</summary>
    public static byte[] SetAlignment(Alignment alignment)
        => new byte[] { 0x1B, 0x61, (byte)alignment };

    /// <summary>ESC E n  ->  bold aç/kapa.</summary>
    public static byte[] SetBold(bool on)
        => new byte[] { 0x1B, 0x45, (byte)(on ? 1 : 0) };

    public enum UnderlineMode : byte { Off = 0, OneDot = 1, TwoDot = 2 }

    /// <summary>ESC - n  ->  altı çizili (kapalı / 1dot / 2dot).</summary>
    public static byte[] SetUnderline(UnderlineMode mode)
        => new byte[] { 0x1B, 0x2D, (byte)mode };

    /// <summary>
    /// GS ! n  ->  karakter boyutu (1-8 katı en, 1-8 katı boy).
    /// width/height parametreleri 1..8 arasında olmalı (1 = normal, 2 = çift, ..., 8 = 8 katı).
    /// </summary>
    public static byte[] SetTextSize(byte width, byte height)
    {
        if (width is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(height));

        // Üst 4 bit: en katı (0..7), alt 4 bit: boy katı (0..7)
        var n = (byte)(((width - 1) << 4) | (height - 1));
        return new byte[] { 0x1D, 0x21, n };
    }

    /// <summary>ESC t n  ->  karakter codepage seç. 32=CP1254 Türkçe, 0=PC437 default.</summary>
    public static byte[] SetCodePage(byte page)
        => new byte[] { 0x1B, 0x74, page };

    /// <summary>ESC R n  ->  uluslararası karakter seti. Türkiye için yakın: 15 (China) yerine codepage tercih edilir.</summary>
    public static byte[] SetInternationalCharSet(byte n)
        => new byte[] { 0x1B, 0x52, n };

    /// <summary>ESC d n  ->  n satır ilerle.</summary>
    public static byte[] FeedLines(byte n) => new byte[] { 0x1B, 0x64, n };

    /// <summary>ESC J n  ->  n dot ilerle.</summary>
    public static byte[] FeedDots(byte n) => new byte[] { 0x1B, 0x4A, n };

    /// <summary>GS V 66 n  ->  n dot ilerle + tam kes (kesim öncesi boşluk garanti).</summary>
    public static byte[] FeedAndCut(byte feedDots)
        => new byte[] { 0x1D, 0x56, 0x42, feedDots };

    // ----- Yüksek seviye yardımcılar -----

    /// <summary>
    /// Metni init + body + LF + cut sandviçi olarak paketler.
    /// Basit, tek-seferlik makbuzlar için.
    /// </summary>
    public static byte[] BuildText(string text, Encoding? encoding = null)
    {
        var enc = encoding ?? Encoding.UTF8;
        var body = enc.GetBytes(text);
        var buffer = new byte[Init.Length + body.Length + LineFeed.Length + CutPaper.Length];
        var offset = 0;

        Init.CopyTo(buffer.AsSpan(offset)); offset += Init.Length;
        body.CopyTo(buffer.AsSpan(offset)); offset += body.Length;
        LineFeed.CopyTo(buffer.AsSpan(offset)); offset += LineFeed.Length;
        CutPaper.CopyTo(buffer.AsSpan(offset));

        return buffer;
    }

    // Görsel için minimal sarmalama: gerçek raster encoding (GS v 0) burada
    // kasıtlı olarak yer almıyor — pluggable kalsın diye sadece init + payload
    // + cut yapıyoruz. Raster dönüşümü Faz 3'te IImageEncoder ile gelecek.
    public static byte[] WrapImagePayload(ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[Init.Length + payload.Length + LineFeed.Length + CutPaper.Length];
        var offset = 0;

        Init.CopyTo(buffer.AsSpan(offset)); offset += Init.Length;
        payload.CopyTo(buffer.AsSpan(offset)); offset += payload.Length;
        LineFeed.CopyTo(buffer.AsSpan(offset)); offset += LineFeed.Length;
        CutPaper.CopyTo(buffer.AsSpan(offset));

        return buffer;
    }

    // ===========================================================================
    // STATUS PARSERS (Cashino KP-300 user manual sayfa 67-69)
    // Her parser, ilgili DLE EOT n yanıtını tek byte alır ve PrinterState? döner.
    // null = bu sorgudan anlamlı bir state çıkmadı (ör. sadece "ready" sinyali).
    // ===========================================================================

    /// <summary>
    /// DLE EOT 1 yanıtı: drawer / online-offline / paper-torn.
    /// Bit 3 = offline durumu. Bağlantı problemi DEĞİL — yazıcı kapağı açık / kağıt yok
    /// gibi durumlarda da offline olur. Bu sorgu Disconnected/Online ayrımı verir.
    /// </summary>
    public static PrinterState? ParsePrinterStatus(byte status)
    {
        // bit 3 set -> offline (genel "şu an basamaz" sinyali)
        // Bunu kendi başına CommError'a haritalamıyoruz; offline sebebini
        // EOT 2/3/4 sorguları söyleyecek. Bu yüzden null dönüyoruz.
        return null;
    }

    /// <summary>
    /// DLE EOT 2 yanıtı: offline status — bit 2 cover, bit 5 paper shortage, bit 6 error flag.
    /// </summary>
    public static PrinterState? ParseOfflineStatus(byte status)
    {
        if ((status & 0x04) != 0) return PrinterState.CoverOpen;
        // bit 5 (0x20) "paper shortage" — paper near-end veya end olabilir; EOT 4 daha kesin söyler.
        // Burada erken karar vermiyoruz; EOT 4 değerlendirmesi devam etsin.
        return null;
    }

    /// <summary>
    /// DLE EOT 3 yanıtı: error status — bit 3 cutter, bit 5 unrecoverable, bit 6 overheat.
    /// </summary>
    public static PrinterState? ParseErrorStatus(byte status)
    {
        // Önce en kritik olan: unrecoverable (genelde voltaj anormal)
        if ((status & 0x20) != 0) return PrinterState.CommError;
        // Sonra cutter sıkışması — "paper jam"e en yakın semantik
        if ((status & 0x08) != 0) return PrinterState.PaperJam;
        // Sonra aşırı ısınma (otomatik recovery)
        if ((status & 0x40) != 0) return PrinterState.Overheat;
        return null;
    }

    /// <summary>
    /// DLE EOT 4 yanıtı: paper sensor — bit 2,3 paper near-end (0x0C), bit 5,6 paper end (0x60).
    /// </summary>
    public static PrinterState? ParsePaperSensorStatus(byte status)
    {
        // Önce kesin kağıt-yok
        if ((status & 0x60) != 0) return PrinterState.PaperOut;
        // Sonra near-end — sistem henüz çalışır ama kullanıcı uyarısı verilebilir.
        // PrinterState enum'unda ayrı bir "PaperNearEnd" yok; şimdilik Ready'i bozmuyoruz.
        // (Domain'e eklemek istersek burada PrinterState.PaperNearEnd döneriz.)
        return null;
    }

    /// <summary>
    /// 4 sorgudan gelen state'leri önceliklendir.
    /// Sıralama (en kötüden iyiye): Disconnected > CommError > PaperOut > PaperJam
    /// > CoverOpen > Overheat > UnknownCommand > Ready.
    /// Hiçbir sorgu anlamlı state üretmediyse (hepsi null), yazıcı çalışıyor demektir -> Ready.
    /// </summary>
    public static PrinterState CombineStates(params PrinterState?[] observed)
    {
        ArgumentNullException.ThrowIfNull(observed);

        PrinterState worst = PrinterState.Ready;
        foreach (var s in observed)
        {
            if (s is null) continue;
            if (Severity(s.Value) > Severity(worst)) worst = s.Value;
        }
        return worst;
    }

    private static int Severity(PrinterState s) => s switch
    {
        PrinterState.Disconnected => 100,
        PrinterState.CommError => 90,
        PrinterState.PaperOut => 80,
        PrinterState.PaperJam => 70,
        PrinterState.CoverOpen => 60,
        PrinterState.Overheat => 50,
        PrinterState.UnknownCommand => 40,
        PrinterState.Ready => 0,
        _ => 0
    };
}
