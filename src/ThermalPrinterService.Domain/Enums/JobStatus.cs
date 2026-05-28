namespace ThermalPrinterService.Domain.Enums;

public enum JobStatus
{
    Queued = 0,
    Printing = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}

public enum JobKind
{
    Text = 0,
    Image = 1,
    Raw = 2     // Önceden ESC/POS byte zincirine kompoze edilmiş tam payload (ör. ReceiptComposer çıktısı)
}
