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
    public const string LabResults = "lab_results";
    public const string SpecimenRejections = "specimen_rejections";
    public const string TestTurnaround = "test_turnaround";
    public const string BloodOrderTurnaround = "blood_order_turnaround";

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
            Field("discard_reason_code", "Reason code"),
            Field("discarded_utc", "Discarded", ReportValueKind.Date),
            Field("discarded_month", "Discard month"),
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
            Field("occurred_month", "Month"),
            Field("event_type", "Event"),
            Field("unit_number", "Unit number"),
            Field("product_name", "Product"),
            Field("mrn", "MRN"),
            Field("detail", "Detail")),
        Source(LabResults, "Lab results", ReportViewCatalog.LabResults,
            Field("mrn", "MRN"),
            Field("patient_name", "Patient"),
            Field("date_of_birth", "Date of birth", ReportValueKind.Date),
            Field("sex", "Sex"),
            Field("patient_status", "Patient status"),
            Field("accession_number", "Accession"),
            Field("specimen_type", "Specimen type"),
            Field("specimen_status", "Specimen status"),
            Field("collected_utc", "Collected", ReportValueKind.Date),
            Field("received_utc", "Received", ReportValueKind.Date),
            Field("expires_utc", "Specimen expiration", ReportValueKind.Date),
            Field("test_code", "Test code"),
            Field("test_name", "Test"),
            Field("test_category", "Test category"),
            Field("result_value", "Result"),
            Field("units", "Units"),
            Field("interpretation", "Interpretation"),
            Field("result_status", "Result status"),
            Field("entered_utc", "Entered", ReportValueKind.Date),
            Field("verified_utc", "Verified", ReportValueKind.Date)),
        Source(SpecimenRejections, "Specimen rejections", ReportViewCatalog.SpecimenRejections,
            Field("rejected_utc", "Rejected", ReportValueKind.Date),
            Field("rejected_month", "Rejection month"),
            Field("rejection_reason_code", "Reason code"),
            Field("rejection_reason", "Reason"),
            Field("rejected_by", "Rejected by"),
            Field("accession_number", "Accession"),
            Field("specimen_type", "Specimen type"),
            Field("draw_location", "Draw location"),
            Field("collector", "Collector"),
            Field("collected_utc", "Collected", ReportValueKind.Date),
            Field("received_utc", "Received", ReportValueKind.Date),
            Field("mrn", "MRN"),
            Field("patient_name", "Patient")),
        // One row per verified current result on an order: minutes from order, and from tube receipt, to verification.
        Source(TestTurnaround, "Test turnaround", ReportViewCatalog.TestTurnaround,
            Field("verified_utc", "Verified", ReportValueKind.Date),
            Field("verified_month", "Verification month"),
            Field("ordered_utc", "Ordered", ReportValueKind.Date),
            Field("received_utc", "Received", ReportValueKind.Date),
            Field("priority", "Priority"),
            Field("test_code", "Test code"),
            Field("order_number", "Order"),
            Field("accession_number", "Accession"),
            Field("minutes_order_to_verify", "Minutes, order to verified", ReportValueKind.Number),
            Field("minutes_receipt_to_verify", "Minutes, receipt to verified", ReportValueKind.Number),
            Field("mrn", "MRN"),
            Field("patient_name", "Patient")),
        // One row per blood order (an order with product lines, not cancelled or discontinued): minutes from order to
        // the first unit reserved and the first issued, and whether a unit was ready by the needed-by time.
        Source(BloodOrderTurnaround, "Blood order turnaround", ReportViewCatalog.BloodOrderTurnaround,
            Field("ordered_utc", "Ordered", ReportValueKind.Date),
            Field("ordered_month", "Order month"),
            Field("priority", "Priority"),
            Field("order_number", "Order"),
            Field("status", "Status"),
            Field("units_ordered", "Units ordered", ReportValueKind.Number),
            Field("first_reserved_utc", "First reserved", ReportValueKind.Date),
            Field("first_issued_utc", "First issued", ReportValueKind.Date),
            Field("minutes_order_to_reserve", "Minutes, order to first reserved", ReportValueKind.Number),
            Field("minutes_order_to_issue", "Minutes, order to first issued", ReportValueKind.Number),
            Field("needed_by_utc", "Needed by", ReportValueKind.Date),
            Field("ready_by_needed_by", "Ready by needed-by"),
            Field("mrn", "MRN"),
            Field("patient_name", "Patient"))
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
