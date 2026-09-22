namespace BloodBankLIS.Domain.Reporting;

/// <summary>
/// The only relations a SQL report may read. Names match the views created
/// for both SQLite and SQL Server.
/// </summary>
public static class ReportViewCatalog
{
    public const string InventoryOnHand = "rpt_inventory_on_hand";
    public const string IssueTransfusion = "rpt_issue_transfusion";
    public const string Discards = "rpt_discards";
    public const string Reactions = "rpt_reactions";
    public const string Lookback = "rpt_lookback";
    public const string Deviations = "rpt_deviations";
    public const string Overrides = "rpt_overrides";
    public const string CrossmatchTransfusion = "rpt_crossmatch_transfusion";
    public const string LabResults = "rpt_lab_results";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        InventoryOnHand,
        IssueTransfusion,
        Discards,
        Reactions,
        Lookback,
        Deviations,
        Overrides,
        CrossmatchTransfusion,
        LabResults
    };
}
