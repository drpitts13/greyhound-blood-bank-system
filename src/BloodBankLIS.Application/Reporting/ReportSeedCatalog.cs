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
            ["unit_number", "product_name", "abo", "rh", "status", "discard_reason_code", "discard_reason", "discarded_utc", "expires_utc"],
            filters: [DateRange("discarded-range", "discarded_utc")],
            sorts: [Sort("discarded_utc", ReportSortDirection.Descending)]),
        Visual("RPT-WASTAGE", "Wastage by product and reason",
            "Count of discarded units by month, product, and discard reason code.",
            ReportSourceCatalog.Discards,
            ["discarded_month", "product_name", "discard_reason_code"],
            filters: [DateRange("discarded-range", "discarded_utc")],
            sorts: [Sort("discarded_month", ReportSortDirection.Descending), Sort("product_name"), Sort("discard_reason_code")],
            groupBy: ["discarded_month", "product_name", "discard_reason_code"],
            aggregates: [new ReportAggregate { Function = ReportAggregateFunction.Count, Alias = "units" }]),
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
            sorts: [Sort("occurred_utc", ReportSortDirection.Descending)]),
        // End-user testing, 2026-11-12: the crossmatch-to-transfusion (C:T) ratio, a standard utilization measure, could
        // not be read from any report. Divide crossmatches by transfusions for the month (and product).
        Visual("RPT-XM-TX-BY-MONTH", "Crossmatches and transfusions by month",
            "Count of crossmatches and of transfusions by month and product, for the crossmatch-to-transfusion (C:T) ratio.",
            ReportSourceCatalog.CrossmatchTransfusion,
            ["occurred_month", "product_name", "event_type"],
            filters: [DateRange("activity-range", "occurred_utc")],
            sorts: [Sort("occurred_month", ReportSortDirection.Descending), Sort("product_name"), Sort("event_type")],
            groupBy: ["occurred_month", "product_name", "event_type"],
            aggregates: [new ReportAggregate { Function = ReportAggregateFunction.Count, Alias = "count" }]),
        Visual("RPT-SPECIMEN-REJECTIONS", "Specimen rejections",
            "Rejected specimens with the coded reason, draw location, collector, and who rejected them.",
            ReportSourceCatalog.SpecimenRejections,
            ["rejected_utc", "rejection_reason_code", "rejection_reason", "accession_number", "mrn", "draw_location", "collector", "rejected_by"],
            filters: [DateRange("rejected-range", "rejected_utc")],
            sorts: [Sort("rejected_utc", ReportSortDirection.Descending)]),
        Visual("RPT-REJECTIONS-BY-CAUSE", "Specimen rejections by cause",
            "Count of rejected specimens by month, reason code, draw location, and collector.",
            ReportSourceCatalog.SpecimenRejections,
            ["rejected_month", "rejection_reason_code", "draw_location", "collector"],
            filters: [DateRange("rejected-range", "rejected_utc")],
            sorts: [Sort("rejected_month", ReportSortDirection.Descending), Sort("rejection_reason_code"), Sort("draw_location"), Sort("collector")],
            groupBy: ["rejected_month", "rejection_reason_code", "draw_location", "collector"],
            aggregates: [new ReportAggregate { Function = ReportAggregateFunction.Count, Alias = "specimens" }]),
        Visual("RPT-TAT", "Test turnaround",
            "Verified results with the minutes from order, and from tube receipt, to verification.",
            ReportSourceCatalog.TestTurnaround,
            ["verified_utc", "priority", "test_code", "order_number", "accession_number", "mrn", "patient_name", "minutes_order_to_verify", "minutes_receipt_to_verify"],
            filters: [DateRange("verified-range", "verified_utc")],
            sorts: [Sort("verified_utc", ReportSortDirection.Descending)]),
        Visual("RPT-TAT-BY-PRIORITY", "Test turnaround by priority",
            "Results verified, average and longest minutes from order to verification, by month, priority, and test.",
            ReportSourceCatalog.TestTurnaround,
            ["verified_month", "priority", "test_code"],
            filters: [DateRange("verified-range", "verified_utc")],
            sorts: [Sort("verified_month", ReportSortDirection.Descending), Sort("priority"), Sort("test_code")],
            groupBy: ["verified_month", "priority", "test_code"],
            aggregates:
            [
                new ReportAggregate { Function = ReportAggregateFunction.Count, Alias = "results" },
                new ReportAggregate { Function = ReportAggregateFunction.Average, Field = "minutes_order_to_verify", Alias = "average minutes" },
                new ReportAggregate { Function = ReportAggregateFunction.Max, Field = "minutes_order_to_verify", Alias = "longest minutes" }
            ]),
        Visual("RPT-BLOOD-ORDER-TAT", "Blood order turnaround",
            "Blood orders with the minutes from order to the first unit reserved and issued, and whether a unit was ready by the needed-by time.",
            ReportSourceCatalog.BloodOrderTurnaround,
            ["ordered_utc", "priority", "order_number", "status", "units_ordered", "mrn", "patient_name", "minutes_order_to_reserve", "minutes_order_to_issue", "needed_by_utc", "ready_by_needed_by"],
            filters: [DateRange("ordered-range", "ordered_utc")],
            sorts: [Sort("ordered_utc", ReportSortDirection.Descending)]),
        Visual("RPT-BLOOD-ORDER-TAT-BY-PRIORITY", "Blood order turnaround by priority",
            "Blood orders, average and longest minutes from order to the first unit reserved, and the average to first issue, by month and priority.",
            ReportSourceCatalog.BloodOrderTurnaround,
            ["ordered_month", "priority"],
            filters: [DateRange("ordered-range", "ordered_utc")],
            sorts: [Sort("ordered_month", ReportSortDirection.Descending), Sort("priority")],
            groupBy: ["ordered_month", "priority"],
            aggregates:
            [
                new ReportAggregate { Function = ReportAggregateFunction.Count, Alias = "orders" },
                new ReportAggregate { Function = ReportAggregateFunction.Average, Field = "minutes_order_to_reserve", Alias = "average minutes to reserve" },
                new ReportAggregate { Function = ReportAggregateFunction.Max, Field = "minutes_order_to_reserve", Alias = "longest minutes to reserve" },
                new ReportAggregate { Function = ReportAggregateFunction.Average, Field = "minutes_order_to_issue", Alias = "average minutes to issue" }
            ])
    ];

    private static ReportDefinition Visual(
        string code,
        string name,
        string description,
        string source,
        IReadOnlyList<string> columns,
        IReadOnlyList<ReportFilter>? filters = null,
        IReadOnlyList<ReportSort>? sorts = null,
        IReadOnlyList<string>? groupBy = null,
        IReadOnlyList<ReportAggregate>? aggregates = null) =>
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
                Sorts = sorts?.ToList() ?? new List<ReportSort>(),
                GroupBy = groupBy?.ToList() ?? new List<string>(),
                Aggregates = aggregates?.ToList() ?? new List<ReportAggregate>()
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
