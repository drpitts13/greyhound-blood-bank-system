using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Application.Reporting;

/// <summary>
/// Field lists for the visual designer. Row filters that define a source
/// (on-hand inventory, open deviations, discards) are applied by the reader.
/// </summary>
public static class ReportSourceCatalog
{
    public const string InventoryOnHand = "inventory_on_hand";
    public const string IssueTransfusion = "issue_transfusion";
    public const string Discards = "discards";
    public const string Reactions = "reactions";
    public const string Lookback = "lookback";
    public const string Deviations = "deviations";
    public const string Overrides = "overrides";
    public const string CrossmatchTransfusion = "crossmatch_transfusion";

    public static readonly IReadOnlyList<ReportSourceDefinition> All = new[]
    {
        Source(InventoryOnHand, "Inventory on hand", ReportViewCatalog.InventoryOnHand,
            Field("unit_number", "Unit number"),
            Field("product_code", "Product code"),
            Field("product_name", "Product"),
            Field("abo", "ABO"),
            Field("rh", "Rh"),
            Field("status", "Status"),
            Field("location", "Location"),
            Field("expires_utc", "Expiration", ReportValueKind.Date)),
        Source(IssueTransfusion, "Issues and transfusions", ReportViewCatalog.IssueTransfusion,
            Field("issued_utc", "Issued", ReportValueKind.Date),
            Field("unit_number", "Unit number"),
            Field("product_name", "Product"),
            Field("abo", "ABO"),
            Field("rh", "Rh"),
            Field("mrn", "MRN"),
            Field("patient_name", "Patient"),
            Field("issue_type", "Issue type"),
            Field("issue_status", "Issue status"),
            Field("transfusion_start_utc", "Transfusion start", ReportValueKind.Date),
            Field("transfusion_stop_utc", "Transfusion stop", ReportValueKind.Date),
            Field("disposition", "Transfusion disposition")),
        Source(Discards, "Discards", ReportViewCatalog.Discards,
            Field("unit_number", "Unit number"),
            Field("product_name", "Product"),
            Field("abo", "ABO"),
            Field("rh", "Rh"),
            Field("status", "Status"),
            Field("discard_reason", "Discard reason"),
            Field("expires_utc", "Expiration", ReportValueKind.Date)),
        Source(Reactions, "Reaction investigations", ReportViewCatalog.Reactions,
            Field("reported_utc", "Reported", ReportValueKind.Date),
            Field("status", "Status"),
            Field("severity", "Severity"),
            Field("reaction_type", "Reaction type"),
            Field("mrn", "MRN"),
            Field("unit_number", "Unit number"),
            Field("product_at_fault", "Product at fault"),
            Field("is_fatality", "Fatality")),
        Source(Lookback, "Lookback notifications", ReportViewCatalog.Lookback,
            Field("din", "DIN"),
            Field("status", "Status"),
            Field("physician", "Physician of record"),
            Field("reason", "Reason"),
            Field("attempted_utc", "Attempted", ReportValueKind.Date),
            Field("mrn", "MRN")),
        Source(Deviations, "Open deviations", ReportViewCatalog.Deviations,
            Field("reported_utc", "Reported", ReportValueKind.Date),
            Field("title", "Title"),
            Field("severity", "Severity"),
            Field("status", "Status"),
            Field("reported_by", "Reported by"),
            Field("context_type", "Context")),
        Source(Overrides, "Emergency releases and overrides", ReportViewCatalog.Overrides,
            Field("occurred_utc", "Occurred", ReportValueKind.Date),
            Field("kind", "Kind"),
            Field("rule_code", "Rule or issue type"),
            Field("reason", "Reason"),
            Field("actor", "Actor"),
            Field("unit_number", "Unit number"),
            Field("mrn", "MRN")),
        Source(CrossmatchTransfusion, "Crossmatch and transfusion activity", ReportViewCatalog.CrossmatchTransfusion,
            Field("occurred_utc", "Occurred", ReportValueKind.Date),
            Field("event_type", "Event"),
            Field("unit_number", "Unit number"),
            Field("product_name", "Product"),
            Field("mrn", "MRN"),
            Field("detail", "Detail"))
    };

    public static ReportSourceDefinition? Find(string? code) =>
        All.FirstOrDefault(source => string.Equals(source.Code, code, StringComparison.OrdinalIgnoreCase));

    private static ReportSourceDefinition Source(string code, string name, string viewName, params ReportField[] fields) =>
        new()
        {
            Code = code,
            Name = name,
            ViewName = viewName,
            Fields = fields
        };

    private static ReportField Field(string code, string label, ReportValueKind kind = ReportValueKind.Text) =>
        new() { Code = code, Label = label, Kind = kind };
}

public sealed class ReportSourceDefinition
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string ViewName { get; init; }
    public required IReadOnlyList<ReportField> Fields { get; init; }
}
