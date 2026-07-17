using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Printing;

/// <summary>Options for a print request. Template/printer default by job type when omitted.</summary>
public sealed record PrintRequest(
    LabelFormat Format = LabelFormat.Zpl,
    string? TemplateCode = null,
    string? TargetPrinter = null);

/// <summary>Read model for a print job (rendered output included on single-job reads).</summary>
public sealed record PrintJobDto(
    long Id,
    PrintJobType JobType,
    string TemplateCode,
    LabelFormat Format,
    string? TargetPrinter,
    string? ContextType,
    long? ContextId,
    PrintJobStatus Status,
    bool IsReprint,
    string? ReprintReason,
    string PrintedBy,
    DateTime? PrintedUtc,
    string? RenderedZpl)
{
    public static PrintJobDto Summary(PrintJob j) => new(
        j.Id, j.JobType, j.TemplateCode, j.Format, j.TargetPrinter, j.ContextType, j.ContextId,
        j.Status, j.IsReprint, j.ReprintReason, j.PrintedBy, j.PrintedUtc, null);

    public static PrintJobDto Full(PrintJob j) => new(
        j.Id, j.JobType, j.TemplateCode, j.Format, j.TargetPrinter, j.ContextType, j.ContextId,
        j.Status, j.IsReprint, j.ReprintReason, j.PrintedBy, j.PrintedUtc, j.RenderedZpl);
}
