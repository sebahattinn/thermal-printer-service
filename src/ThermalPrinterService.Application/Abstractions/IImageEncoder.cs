namespace ThermalPrinterService.Application.Abstractions;

/// <summary>
/// Ham bir görseli (PNG, JPEG, BMP vb.) termal yazıcının anladığı
/// ESC/POS raster bitmap byte zincirine çevirir.
/// </summary>
public interface IImageEncoder
{
    /// <summary>
    /// <paramref name="imageBytes"/> içindeki görseli decode edip,
    /// belirtilen yazıcı genişliğine sığacak şekilde ölçekleyip,
    /// 1bpp monochrome bitmap'e çevirip ESC/POS raster (GS v 0) byte zincirini döner.
    /// </summary>
    /// <param name="imageBytes">PNG / JPEG / BMP byte verisi.</param>
    /// <param name="maxWidthDots">
    /// Yazıcının max yazdırma genişliği (dot). KP-300 için 384, KP-302/301H için 576/640.
    /// </param>
    byte[] Encode(ReadOnlySpan<byte> imageBytes, int maxWidthDots);
}
