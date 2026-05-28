using ThermalPrinterService.Application.Receipts;

namespace ThermalPrinterService.Application.Abstractions;

/// <summary>
/// ReceiptDocument'i belirli bir yazıcı için ESC/POS byte zincirine kompoze eder.
/// </summary>
public interface IReceiptComposer
{
    /// <summary>
    /// Composer yazıcı genişliğini kendi yapılandırmasından çözer; çağıran tarafta
    /// boyut bilgisi gerekmez.
    /// </summary>
    byte[] Compose(ReceiptDocument document);
}
