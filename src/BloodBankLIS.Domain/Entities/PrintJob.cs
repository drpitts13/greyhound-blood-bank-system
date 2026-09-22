using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A record of a label/tag print (table <c>PrintJobs</c>). Captures the exact data
/// model (PayloadJson) and rendered output (RenderedZpl) so what was printed is
/// reconstructable. Reprints require a reason and set <see cref="IsReprint"/> +
/// <see cref="ReprintReason"/> (see docs/printing-billing.md A.3).
/// </summary>
public class PrintJob : BaseEntity
{
    public PrintJobType JobType { get; set; }

    public string TemplateCode { get; set; } = string.Empty;

    public LabelFormat Format { get; set; } = LabelFormat.Zpl;

    public string? TargetPrinter { get; set; }

    /// <summary>Entity the job was generated from (e.g. Specimen, Issue), for traceability.</summary>
    public string? ContextType { get; set; }

    public long? ContextId { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public string? RenderedZpl { get; set; }

    /// <summary>PDF bytes when <see cref="Format"/> is <see cref="LabelFormat.Pdf"/>.</summary>
    public byte[]? RenderedPdf { get; set; }

    public PrintDeliveryStatus DeliveryStatus { get; set; } = PrintDeliveryStatus.NotSent;

    public string? DeliveryError { get; set; }

    public DateTime? DeliveredUtc { get; set; }

    public long? PdfPrinterId { get; set; }

    public long? ZebraPrinterId { get; set; }

    public PrintJobStatus Status { get; set; } = PrintJobStatus.Queued;

    public bool IsReprint { get; set; }

    public string? ReprintReason { get; set; }

    public string PrintedBy { get; set; } = "system";

    public DateTime? PrintedUtc { get; set; }
}
