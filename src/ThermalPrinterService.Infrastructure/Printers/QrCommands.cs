using System.Text;

namespace ThermalPrinterService.Infrastructure.Printers;

public enum QrErrorCorrection : byte
{
    // ESC/POS standart değerler (KP-300 manuel sayfa 58)
    L = 48, // ~7%
    M = 49, // ~15% — varsayılan, çoğu fiş için yeterli
    Q = 50, // ~25%
    H = 51  // ~30% — logo basılmış QR veya açık havada okuma için
}

public enum QrModel : byte
{
    Model1 = 49,
    Model2 = 50 // varsayılan, modern QR okuyucular bunu ister
}

/// <summary>
/// ESC/POS GS ( k QR kod komut zinciri (KP-300/301H/302 uyumlu).
/// Tek bir Build() ile 5 alt komut sırayla paketlenir:
///   1) model seç      GS ( k 04 00 31 41 n 00
///   2) modül boyutu   GS ( k 03 00 31 43 n
///   3) ECC seviyesi   GS ( k 03 00 31 45 n
///   4) veri depola    GS ( k pL pH 31 50 30 d1..dk
///   5) yazdır         GS ( k 03 00 31 51 30
/// </summary>
public static class QrCommands
{
    // En düşük modül boyutu — 1 dot/modül, küçük QR'lar
    public const byte MinModuleSize = 1;

    // ESC/POS standardı 16'ya kadar destekler; çoğu pratikte 4-8 arası okunur boyut
    public const byte MaxModuleSize = 16;

    /// <summary>
    /// QR kod komut zincirinin tamamını üretir.
    /// </summary>
    /// <param name="data">QR'a kodlanacak metin (UTF-8 byte'larına çevrilir).</param>
    /// <param name="moduleSize">Bir modülün kaç dot olduğu (1..16). Pratikte 4-8 arası okunur.</param>
    /// <param name="ecc">Hata düzeltme seviyesi.</param>
    /// <param name="model">QR Model 1 veya 2 (varsayılan Model 2).</param>
    public static byte[] Build(
        string data,
        byte moduleSize = 6,
        QrErrorCorrection ecc = QrErrorCorrection.M,
        QrModel model = QrModel.Model2)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0) throw new ArgumentException("QR data boş olamaz.", nameof(data));
        if (moduleSize is < MinModuleSize or > MaxModuleSize)
            throw new ArgumentOutOfRangeException(nameof(moduleSize),
                $"moduleSize {MinModuleSize}..{MaxModuleSize} arasında olmalı.");

        var payload = Encoding.UTF8.GetBytes(data);

        // pL/pH veri uzunluğunun + 3 byte header'ın (cn fn m = 31 50 30) toplamıdır.
        // Spec: pL + pH*256 = data.Length + 3 ; data ESC/POS toplam 7092 byte ile sınırlı.
        if (payload.Length > 7089)
            throw new ArgumentException(
                $"QR data 7089 byte'tan büyük olamaz (verilen: {payload.Length}).", nameof(data));

        var selectModel = SelectModel(model);
        var size = SetModuleSize(moduleSize);
        var correction = SetErrorCorrection(ecc);
        var store = StoreData(payload);
        var print = PrintBuffer;

        var total = selectModel.Length + size.Length + correction.Length + store.Length + print.Length;
        var output = new byte[total];
        var offset = 0;
        foreach (var chunk in new[] { selectModel, size, correction, store, print })
        {
            chunk.CopyTo(output, offset);
            offset += chunk.Length;
        }
        return output;
    }

    // ----- Alt komutlar (internal API, test edilebilir olsun diye public bırakıldı) -----

    public static byte[] SelectModel(QrModel model) =>
        new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, (byte)model, 0x00 };

    public static byte[] SetModuleSize(byte size) =>
        new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, size };

    public static byte[] SetErrorCorrection(QrErrorCorrection level) =>
        new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, (byte)level };

    public static byte[] StoreData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        // (pL + pH*256) = data.Length + 3 (cn=31 fn=50 m=30 header'ı dahil)
        var fullLen = data.Length + 3;
        var pL = (byte)(fullLen & 0xFF);
        var pH = (byte)((fullLen >> 8) & 0xFF);

        var output = new byte[8 + data.Length];
        output[0] = 0x1D; output[1] = 0x28; output[2] = 0x6B;
        output[3] = pL; output[4] = pH;
        output[5] = 0x31; output[6] = 0x50; output[7] = 0x30;
        Array.Copy(data, 0, output, 8, data.Length);
        return output;
    }

    public static byte[] PrintBuffer =>
        new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 };
}
