using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Application.Reporting;

/// <summary>
/// Prebuilt operational reports. This is a facility starter set pending regulatory
/// review. It is not an FDA, AABB, or CAP compliance claim.
/// </summary>
public static class ReportSeedCatalog
{
    public static IReadOnlyList<ReportDefinition> Create() =>
    [
        Visual("RPT-INVENTORY", "Inventory on hand",
            "Units still in inventory, with product, ABO/Rh, location, and expiration.",
            ReportSourceCatalog.InventoryOnHand,
            ["unit_number", "product_name", "abo", "rh", "status", "location", "expires_utc"],
            sorts: [Sort("expires_utc")]),
        Visual("RPT-ISSUE-TRANSFUSE", "Units issued and transfused",
            "Issues in a date range, with transfusion times when a transfusion was documented.",
            ReportSourceCatalog.IssueTransfusion,
            ["issued_utc", "unit_number", "product_name", "mrn", "patient_name", "issue_type", "transfusion_start_utc", "disposition"],
            filters: [DateRange("issued-range", "issued_utc")],
            sorts: [Sort("issued_utc", ReportSortDirection.Descending)]),
        Visual("RPT-DISCARDS", "Discards and waste",
            "Units discarded or recorded with a discard reason.",
            ReportSourceCatalog.Discards,
            ["unit_number", "product_name", "abo", "rh", "status", "discard_reason", "expires_utc"]),
        Visual("RPT-REACTIONS", "Transfusion reaction investigations",
            "Open and closed reaction investigations.",
            ReportSourceCatalog.Reactions,
            ["reported_utc", "status", "severity", "reaction_type", "mrn", "unit_number", "product_at_fault", "is_fatality"],
            filters: [DateRange("reported-range", "reported_utc")],
            sorts: [Sort("reported_utc", ReportSortDirection.Descending)]),
        Visual("RPT-LOOKBACK", "Lookback and recall notifications",
            "Recipient notification rows recorded for a DIN lookback.",
            ReportSourceCatalog.Lookback,
            ["din", "status", "mrn", "physician", "reason", "attempted_utc"],
            sorts: [Sort("din")]),
        Visual("RPT-DEVIATIONS", "Open deviations",
            "Deviations that are not closed.",
            ReportSourceCatalog.Deviations,
            ["reported_utc", "title", "severity", "status", "reported_by", "context_type"],
            sorts: [Sort("reported_utc", ReportSortDirection.Descending)]),
        Visual("RPT-OVERRIDES", "Emergency releases and overrides",
            "Warning overrides plus emergency and massive-transfusion issues.",
            ReportSourceCatalog.Overrides,
            ["occurred_utc", "kind", "rule_code", "reason", "actor", "unit_number", "mrn"],
            filters: [DateRange("occurred-range", "occurred_utc")],
            sorts: [Sort("occurred_utc", ReportSortDirection.Descending)]),
        Visual("RPT-XM-TX", "Crossmatch and transfusion activity",
            "Crossmatches and transfusions, so a count by event type can be grouped in the designer.",
            ReportSourceCatalog.CrossmatchTransfusion,
            ["occurred_utc", "event_type", "unit_number", "product_name", "mrn", "detail"],
            filters: [DateRange("activity-range", "occurred_utc")],
            sorts: [Sort("occurred_utc", ReportSortDirection.Descending)])
    ];

    private static ReportDefinition Visual(
        string code,
        string name,
        string description,
        string source,
        IReadOnlyList<string> columns,
        IReadOnlyList<ReportFilter>? filters = null,
        IReadOnlyList<ReportSort>? sorts = null) =>
        new()
        {
            Code = code,
            Name = name,
            Description = description,
            Kind = ReportKind.Visual,
            IsSystemOwned = true,
            LayoutJson = ReportLayoutJson.Serialize(new ReportLayout
            {
                SourceCode = source,
                Columns = columns.ToList(),
                Filters = filters?.ToList() ?? new List<ReportFilter>(),
                Sorts = sorts?.ToList() ?? new List<ReportSort>()
            })
        };

    private static ReportFilter DateRange(string id, string field) =>
        new()
        {
            Id = id,
            Field = field,
            Operator = ReportFilterOperator.Between,
            PromptAtRun = true
        };

    private static ReportSort Sort(string field, ReportSortDirection direction = ReportSortDirection.Ascending) =>
        new() { Field = field, Direction = direction };
}
