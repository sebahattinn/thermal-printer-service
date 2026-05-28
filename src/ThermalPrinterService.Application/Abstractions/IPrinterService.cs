using ThermalPrinterService.Application.DTOs;
using ThermalPrinterService.Domain.Enums;

namespace ThermalPrinterService.Application.Abstractions;

/// <summary>
/// Fiziksel yazıcı ile konuşan en alt seviyeli sözleşme.
/// Her bağlantı türü (USB, LAN) bu arayüzü ayrı olarak gerçekler.
/// Bu arayüz <b>kuyruk yönetmez</b>; yalnızca tek bir gönderim yapar.
/// Kuyrukla ilgili akış IPrintQueue + PrintWorker üzerinden işler.
/// </summary>
public interface IPrinterService
{
    /// <summary>Sürücünün hangi modda çalıştığını belirtir.</summary>
    ConnectionMode Mode { get; }

    /// <summary>Aktif bağlantı durumu (snapshot).</summary>
    bool IsConnected { get; }

    /// <summary>State machine tarafından raporlanan son durum.</summary>
    PrinterState CurrentState { get; }

    /// <summary>
    /// Bağlantıyı açar. Çağıran tarafa <c>reconnect</c> mantığı dahil değildir;
    /// arka plan servisi (ReconnectBackgroundService) bu metodu backoff ile sarmalar.
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Bağlantıyı düzgünce kapatır; idempotent olmalıdır.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Yazıcının canlı durumunu sorgular (kağıt, kapak, sıcaklık, iletişim).
    /// State machine bu metodun sonucunu kullanarak <see cref="CurrentState"/>'i günceller.
    /// </summary>
    Task<PrinterState> QueryStateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Tek bir metin işini fiziksel olarak basar.
    /// Hata fırlatırsa PrintWorker bunu yakalayıp yeniden deneme / loglama yapar.
    /// </summary>
    Task PrintTextAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Tek bir görsel işini fiziksel olarak basar.
    /// Görüntü decode/scaling sorumluluğu implementasyona aittir.
    /// </summary>
    Task PrintImageAsync(ReadOnlyMemory<byte> imageBytes, CancellationToken cancellationToken);

    /// <summary>
    /// Önceden ESC/POS byte zincirine kompoze edilmiş tam payload'u sarmadan
    /// (init/cut eklemeden) yazıcıya gönderir. ReceiptComposer çıktısı için kullanılır.
    /// </summary>
    Task PrintRawAsync(ReadOnlyMemory<byte> rawBytes, CancellationToken cancellationToken);
}

/// <summary>
/// Mode (USB/LAN) -&gt; concrete <see cref="IPrinterService"/> üreten fabrika.
/// Controller'in implementasyonları doğrudan tanımasına gerek kalmaz.
/// </summary>
public interface IPrinterFactory
{
    IPrinterService Create(ConnectionMode mode);
}

/// <summary>
/// Connect sonrası seçilen aktif yazıcı örneğini DI scope'undan ayırarak
/// uygulama yaşam döngüsünde tek bir aktif sürücü tutar.
/// </summary>
public interface IPrinterSession
{
    IPrinterService? Current { get; }
    void Set(IPrinterService printer);
    void Clear();
}
