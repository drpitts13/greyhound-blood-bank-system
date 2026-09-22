using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A Windows print queue on the computer that runs the API (local or already-connected network printer).
/// PDF jobs can be sent here after preview. The queue is not discovered by scanning the network.
/// </summary>
public class PdfPrinter : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string WindowsPrinterName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }

    public string? Notes { get; set; }
}
