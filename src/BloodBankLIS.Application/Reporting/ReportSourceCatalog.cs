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
    public const string Rhig = "rhig";
    public const string ReagentQc = "reagent_qc";
    public const string EquipmentChecks = "equipment_checks";

    public static readonly IReadOnlyList<ReportSourceDefinition> All = new[]
    {
        Source(InventoryOnHand, "Inventory on hand", ReportViewCatalog.InventoryOnHand,
            Field("unit_number", "Unit number"),
            Field("product_code", "Product code"),
            Field("product_name", "Product"),
            Field("abo", "ABO"),
            Field("rh", "Rh"),
            Field("status", "Status", codeName: true, unitStatus: true),
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
            Field("issue_type", "Issue type", codeName: true),
            Field("issue_status", "Issue status", codeName: true),
            Field("transfusion_start_utc", "Transfusion start", ReportValueKind.Date),
            Field("transfusion_stop_utc", "Transfusion stop", ReportValueKind.Date),
            Field("disposition", "Transfusion disposition")),
        Source(Discards, "Discards", ReportViewCatalog.Discards,
            Field("unit_number", "Unit number"),
            Field("product_name", "Product"),
            Field("abo", "ABO"),
            Field("rh", "Rh"),
            Field("status", "Status", codeName: true, unitStatus: true),
            Field("discard_reason", "Discard reason"),
            Field("discard_reason_code", "Reason code"),
            Field("discarded_utc", "Discarded", ReportValueKind.Date),
            Field("discarded_month", "Discard month"),
            Field("expires_utc", "Expiration", ReportValueKind.Date)),
        Source(Reactions, "Reaction investigations", ReportViewCatalog.Reactions,
            Field("reported_utc", "Reported", ReportValueKind.Date),
            Field("status", "Status", codeName: true),
            Field("severity", "Severity", codeName: true),
            Field("reaction_type", "Reaction type", codeName: true),
            Field("mrn", "MRN"),
            Field("unit_number", "Unit number"),
            Field("product_at_fault", "Product at fault"),
            Field("is_fatality", "Fatality")),
        // Pass 121: RhIG evaluations and doses given, for the review of RhIG practice.
        Source(Rhig, "RhIG evaluations", ReportViewCatalog.Rhig,
            Field("event_utc", "Event", ReportValueKind.Date),
            Field("context", "Event type"),
            Field("mrn", "MRN"),
            Field("decision", "Decision", codeName: true),
            Field("vials_recommended", "Vials recommended"),
            Field("completed_utc", "Completed", ReportValueKind.Date),
            Field("given_utc", "Given", ReportValueKind.Date),
            Field("given_vials", "Vials given"),
            Field("given_lot", "Lot"),
            Field("given_by", "Given by"),
            Field("hours_event_to_given", "Hours from event to given")),
        // Pass 139: every reagent QC reading, for the QC record of a period (the Reagents page shows 14 days).
        Source(ReagentQc, "Reagent QC", ReportViewCatalog.ReagentQc,
            Field("performed_utc", "Performed", ReportValueKind.Date),
            Field("reagent_code", "Reagent"),
            Field("reagent_name", "Reagent name"),
            Field("lot_number", "Lot"),
            Field("lot_expires_utc", "Lot expires", ReportValueKind.Date),
            Field("control", "Control"),
            Field("expected_result", "Expected"),
            Field("observed_result", "Observed"),
            Field("result", "Result"),
            Field("performed_by", "Performed by"),
            Field("reviewed_by", "Reviewed by"),
            Field("reviewed_utc", "Reviewed", ReportValueKind.Date)),
        // Pass 140: every equipment check recorded, for the maintenance record of a period across all equipment.
        Source(EquipmentChecks, "Equipment checks", ReportViewCatalog.EquipmentChecks,
            Field("performed_utc", "Performed", ReportValueKind.Date),
            Field("equipment_code", "Equipment"),
            Field("equipment_name", "Equipment name"),
            Field("kind", "Kind", codeName: true),
            Field("location", "Location"),
            Field("serial_number", "Serial number"),
            Field("check_name", "Check"),
            Field("result", "Result"),
            Field("performed_by", "Performed by"),
            Field("note", "Note")),
        Source(Lookback, "Lookback notifications", ReportViewCatalog.Lookback,
            Field("din", "DIN"),
            Field("status", "Status", codeName: true),
            Field("physician", "Physician of record"),
            Field("reason", "Reason"),
            Field("attempted_utc", "Attempted", ReportValueKind.Date),
            Field("mrn", "MRN"),
            // Pass 246: the unit transfused and when the notification was opened, as the Lookback page shows them.
            Field("unit_number", "Unit number"),
            Field("opened_utc", "Opened", ReportValueKind.Date)),
        Source(Deviations, "Open deviations", ReportViewCatalog.Deviations,
            Field("reported_utc", "Reported", ReportValueKind.Date),
            Field("title", "Title"),
            Field("severity", "Severity", codeName: true),
            Field("status", "Status", codeName: true),
            Field("reported_by", "Reported by"),
            Field("context_type", "Context", codeName: true),
            // Pass 247: which record, who has it, when it is due, and FDA reportability, as the Deviations page shows them.
            Field("record", "Related record"),
            Field("assigned_to", "Assigned to"),
            Field("due_on", "Due", ReportValueKind.Date),
            Field("fda_reportability", "FDA reportability", codeName: true)),
        Source(Overrides, "Emergency releases and overrides", ReportViewCatalog.Overrides,
            Field("occurred_utc", "Occurred", ReportValueKind.Date),
            Field("kind", "Kind", codeName: true),
            Field("rule_code", "Rule or issue type", codeName: true),
            Field("reason", "Reason"),
            Field("actor", "Actor"),
            Field("unit_number", "Unit number"),
            Field("mrn", "MRN"),
            // Pass 120: for a release before testing was complete, whether the physician's statement was received.
            Field("physician_statement", "Physician statement")),
        Source(CrossmatchTransfusion, "Crossmatch and transfusion activity", ReportViewCatalog.CrossmatchTransfusion,
            Field("occurred_utc", "Occurred", ReportValueKind.Date),
            Field("occurred_month", "Month"),
            Field("event_type", "Event", codeName: true),
            Field("unit_number", "Unit number"),
            Field("product_name", "Product"),
            Field("mrn", "MRN"),
            Field("detail", "Detail")),
        Source(LabResults, "Lab results", ReportViewCatalog.LabResults,
            Field("mrn", "MRN"),
            Field("patient_name", "Patient"),
            Field("date_of_birth", "Date of birth", ReportValueKind.Date),
            Field("sex", "Sex"),
            Field("patient_status", "Patient status", codeName: true),
            Field("accession_number", "Accession"),
            Field("specimen_type", "Specimen type"),
            Field("specimen_status", "Specimen status", codeName: true),
            Field("collected_utc", "Collected", ReportValueKind.Date),
            Field("received_utc", "Received", ReportValueKind.Date),
            Field("expires_utc", "Specimen expiration", ReportValueKind.Date),
            Field("test_code", "Test code"),
            Field("test_name", "Test"),
            Field("test_category", "Test category"),
            Field("result_value", "Result"),
            Field("units", "Units"),
            Field("interpretation", "Interpretation"),
            Field("result_status", "Result status", codeName: true),
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
            Field("status", "Status", codeName: true),
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

    private static ReportField Field(string code, string label, ReportValueKind kind = ReportValueKind.Text, bool codeName = false, bool unitStatus = false) =>
        new() { Code = code, Label = label, Kind = kind, IsCodeName = codeName, IsUnitStatus = unitStatus };
}

public sealed class ReportSourceDefinition
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string ViewName { get; init; }
    public required IReadOnlyList<ReportField> Fields { get; init; }
}
