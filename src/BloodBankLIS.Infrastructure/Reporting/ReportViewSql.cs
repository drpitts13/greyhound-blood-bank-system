using System.Globalization;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Infrastructure.Reporting;

/// <summary>
/// Reporting views shared by SQLite and SQL Server. The SELECT lists use the same
/// column names as the visual report source catalog.
/// </summary>
public static class ReportViewSql
{
    public static void Apply(Action<string> execute, string provider)
    {
        var sqlServer = provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);
        foreach (var (name, body) in Definitions(sqlServer))
        {
            if (sqlServer)
            {
                execute($"CREATE OR ALTER VIEW {name} AS {body}");
                continue;
            }

            execute($"DROP VIEW IF EXISTS {name}");
            execute($"CREATE VIEW {name} AS {body}");
        }
    }

    public static void Drop(Action<string> execute)
    {
        foreach (var name in ReportViewCatalog.All)
            execute($"DROP VIEW IF EXISTS {name}");
    }

    private static IEnumerable<(string Name, string Body)> Definitions(bool sqlServer)
    {
        var onHand = string.Join(", ", ReportInventoryScope.OnHand.Select(status => ((int)status).ToString(CultureInfo.InvariantCulture)));
        var join = sqlServer ? " + " : " || ";
        var person = sqlServer
            ? "COALESCE(p.LastName, '') + CASE WHEN p.LastName IS NULL OR p.FirstName IS NULL OR p.LastName = '' OR p.FirstName = '' THEN '' ELSE ', ' END + COALESCE(p.FirstName, '')"
            : "COALESCE(p.LastName, '') || CASE WHEN p.LastName IS NULL OR p.FirstName IS NULL OR p.LastName = '' OR p.FirstName = '' THEN '' ELSE ', ' END || COALESCE(p.FirstName, '')";
        var crossmatchDetail =
            $"{CaseEnum("c.Method", typeof(CrossmatchMethod))}{join}' '{join}{CaseEnum("c.Result", typeof(CrossmatchResult))}";
        var dateOfBirth = sqlServer ? "CONVERT(varchar(10), p.DateOfBirth, 23)" : "p.DateOfBirth";
        var activeTest = "td.Code = r.TestCode AND td.IsActive = 1 AND td.IsDraft = 0";
        var discardedMonth = sqlServer ? "CONVERT(varchar(7), h.DiscardedUtc, 126)" : "substr(h.DiscardedUtc, 1, 7)";
        var crossmatchMonth = sqlServer ? "CONVERT(varchar(7), c.PerformedUtc, 126)" : "substr(c.PerformedUtc, 1, 7)";
        var transfusionMonth = sqlServer ? "CONVERT(varchar(7), t.StartUtc, 126)" : "substr(t.StartUtc, 1, 7)";
        // Specimens rejected before RejectedUtc was recorded fall back to their last modification time.
        var rejectedAt = "COALESCE(s.RejectedUtc, s.ModifiedUtc)";
        var rejectedMonth = sqlServer ? $"CONVERT(varchar(7), {rejectedAt}, 126)" : $"substr({rejectedAt}, 1, 7)";
        var testName = sqlServer
            ? $"(SELECT TOP 1 td.Name FROM TestDefinitions td WHERE {activeTest} ORDER BY td.Id DESC)"
            : $"(SELECT td.Name FROM TestDefinitions td WHERE {activeTest} ORDER BY td.Id DESC LIMIT 1)";
        var testCategory = sqlServer
            ? $"(SELECT TOP 1 {CaseEnum("td.Category", typeof(TestCategory))} FROM TestDefinitions td WHERE {activeTest} ORDER BY td.Id DESC)"
            : $"(SELECT {CaseEnum("td.Category", typeof(TestCategory))} FROM TestDefinitions td WHERE {activeTest} ORDER BY td.Id DESC LIMIT 1)";

        yield return (ReportViewCatalog.InventoryOnHand, $"""
            SELECT
                u.UnitNumber AS unit_number,
                pt.ProductCode AS product_code,
                pt.Name AS product_name,
                {CaseEnum("u.Abo", typeof(AboGroup))} AS abo,
                {CaseEnum("u.RhD", typeof(RhType))} AS rh,
                {CaseEnum("u.Status", typeof(UnitStatus))} AS status,
                loc.Name AS location,
                u.ExpiresUtc AS expires_utc
            FROM BloodProducts u
            INNER JOIN ProductTypes pt ON pt.Id = u.ProductTypeId
            LEFT JOIN InventoryLocations loc ON loc.Id = u.CurrentLocationId
            WHERE u.Status IN ({onHand})
            """);

        yield return (ReportViewCatalog.IssueTransfusion, $"""
            SELECT
                i.IssuedUtc AS issued_utc,
                u.UnitNumber AS unit_number,
                pt.Name AS product_name,
                {CaseEnum("u.Abo", typeof(AboGroup))} AS abo,
                {CaseEnum("u.RhD", typeof(RhType))} AS rh,
                p.MedicalRecordNumber AS mrn,
                {person} AS patient_name,
                {CaseEnum("i.IssueType", typeof(IssueType))} AS issue_type,
                {CaseEnum("i.Status", typeof(IssueStatus))} AS issue_status,
                t.StartUtc AS transfusion_start_utc,
                t.StopUtc AS transfusion_stop_utc,
                CASE WHEN t.Id IS NULL THEN NULL ELSE {TransfusionRecordState.SqlDisposition("t.StopUtc", "i.Status", CaseEnum("t.FinalDisposition", typeof(TransfusionDisposition)))} END AS disposition
            FROM Issues i
            INNER JOIN BloodProducts u ON u.Id = i.BloodProductId
            INNER JOIN ProductTypes pt ON pt.Id = u.ProductTypeId
            INNER JOIN Patients p ON p.Id = i.PatientId
            LEFT JOIN TransfusionEvents t ON t.IssueId = i.Id
            """);

        yield return (ReportViewCatalog.Discards, $"""
            SELECT
                u.UnitNumber AS unit_number,
                pt.Name AS product_name,
                {CaseEnum("u.Abo", typeof(AboGroup))} AS abo,
                {CaseEnum("u.RhD", typeof(RhType))} AS rh,
                {CaseEnum("u.Status", typeof(UnitStatus))} AS status,
                u.DiscardReason AS discard_reason,
                u.DiscardReasonCode AS discard_reason_code,
                h.DiscardedUtc AS discarded_utc,
                {discardedMonth} AS discarded_month,
                u.ExpiresUtc AS expires_utc
            FROM BloodProducts u
            INNER JOIN ProductTypes pt ON pt.Id = u.ProductTypeId
            LEFT JOIN (
                SELECT BloodProductId, MAX(ChangedUtc) AS DiscardedUtc
                FROM InventoryStatusHistory
                WHERE ToStatus = {(int)UnitStatus.Discarded}
                GROUP BY BloodProductId
            ) h ON h.BloodProductId = u.Id
            WHERE u.Status = {(int)UnitStatus.Discarded} OR u.DiscardReason IS NOT NULL
            """);

        yield return (ReportViewCatalog.Reactions, $"""
            SELECT
                r.ReportedUtc AS reported_utc,
                {CaseEnum("r.Status", typeof(ReactionInvestigationStatus))} AS status,
                {CaseEnum("r.Severity", typeof(ReactionSeverity))} AS severity,
                r.ReactionType AS reaction_type,
                p.MedicalRecordNumber AS mrn,
                u.UnitNumber AS unit_number,
                CASE WHEN r.ProductAtFault = 1 THEN 'Yes' ELSE 'No' END AS product_at_fault,
                CASE WHEN r.IsFatality = 1 THEN 'Yes' ELSE 'No' END AS is_fatality
            FROM ReactionInvestigations r
            INNER JOIN Patients p ON p.Id = r.PatientId
            INNER JOIN BloodProducts u ON u.Id = r.BloodProductId
            """);

        yield return (ReportViewCatalog.Lookback, $"""
            SELECT
                n.Din AS din,
                {CaseEnum("n.Status", typeof(LookbackNotificationStatus))} AS status,
                n.PhysicianOfRecord AS physician,
                n.Reason AS reason,
                n.AttemptedUtc AS attempted_utc,
                p.MedicalRecordNumber AS mrn
            FROM LookbackNotifications n
            LEFT JOIN Patients p ON p.Id = n.PatientId
            """);

        yield return (ReportViewCatalog.Deviations, $"""
            SELECT
                d.ReportedUtc AS reported_utc,
                d.Title AS title,
                {CaseEnum("d.Severity", typeof(DeviationSeverity))} AS severity,
                {CaseEnum("d.Status", typeof(DeviationStatus))} AS status,
                d.ReportedBy AS reported_by,
                d.ContextType AS context_type
            FROM Deviations d
            WHERE d.Status <> {(int)DeviationStatus.Closed}
            """);

        yield return (ReportViewCatalog.Overrides, $"""
            SELECT
                o.OverriddenUtc AS occurred_utc,
                {CaseEnum("o.Action", typeof(OverrideAction))} AS kind,
                o.RuleCode AS rule_code,
                o.Reason AS reason,
                o.AuthorizedBy AS actor,
                CAST(NULL AS {TextType(sqlServer)}) AS unit_number,
                CAST(NULL AS {TextType(sqlServer)}) AS mrn
            FROM Overrides o
            UNION ALL
            SELECT
                i.IssuedUtc AS occurred_utc,
                {CaseEnum("i.IssueType", typeof(IssueType))} AS kind,
                {CaseEnum("i.IssueType", typeof(IssueType))} AS rule_code,
                i.EmergencyReleaseDetails AS reason,
                i.IssuedBy AS actor,
                u.UnitNumber AS unit_number,
                p.MedicalRecordNumber AS mrn
            FROM Issues i
            INNER JOIN BloodProducts u ON u.Id = i.BloodProductId
            INNER JOIN Patients p ON p.Id = i.PatientId
            WHERE i.IssueType IN ({(int)IssueType.EmergencyRelease}, {(int)IssueType.MassiveTransfusion})
            """);

        yield return (ReportViewCatalog.CrossmatchTransfusion, $"""
            SELECT
                c.PerformedUtc AS occurred_utc,
                {crossmatchMonth} AS occurred_month,
                'Crossmatch' AS event_type,
                u.UnitNumber AS unit_number,
                pt.Name AS product_name,
                p.MedicalRecordNumber AS mrn,
                {crossmatchDetail} AS detail
            FROM Crossmatches c
            INNER JOIN BloodProducts u ON u.Id = c.BloodProductId
            INNER JOIN ProductTypes pt ON pt.Id = u.ProductTypeId
            INNER JOIN Patients p ON p.Id = c.PatientId
            UNION ALL
            SELECT
                t.StartUtc AS occurred_utc,
                {transfusionMonth} AS occurred_month,
                'Transfusion' AS event_type,
                u.UnitNumber AS unit_number,
                pt.Name AS product_name,
                p.MedicalRecordNumber AS mrn,
                {TransfusionRecordState.SqlDisposition("t.StopUtc", "ti.Status", CaseEnum("t.FinalDisposition", typeof(TransfusionDisposition)))} AS detail
            FROM TransfusionEvents t
            INNER JOIN Issues ti ON ti.Id = t.IssueId
            INNER JOIN BloodProducts u ON u.Id = t.BloodProductId
            INNER JOIN ProductTypes pt ON pt.Id = u.ProductTypeId
            INNER JOIN Patients p ON p.Id = t.PatientId
            """);

        yield return (ReportViewCatalog.LabResults, $"""
            SELECT
                p.MedicalRecordNumber AS mrn,
                {person} AS patient_name,
                {dateOfBirth} AS date_of_birth,
                {CaseEnum("p.Sex", typeof(Sex))} AS sex,
                {CaseEnum("p.Status", typeof(PatientStatus))} AS patient_status,
                s.AccessionNumber AS accession_number,
                s.SpecimenType AS specimen_type,
                {CaseEnum("s.Status", typeof(SpecimenStatus))} AS specimen_status,
                s.CollectedUtc AS collected_utc,
                s.ReceivedUtc AS received_utc,
                s.ExpiresUtc AS expires_utc,
                r.TestCode AS test_code,
                {testName} AS test_name,
                {testCategory} AS test_category,
                r.Value AS result_value,
                r.Units AS units,
                r.Interpretation AS interpretation,
                {CaseEnum("r.Status", typeof(ResultStatus))} AS result_status,
                r.EnteredUtc AS entered_utc,
                r.VerifiedUtc AS verified_utc
            FROM TestResults r
            INNER JOIN Patients p ON p.Id = r.PatientId
            INNER JOIN Specimens s ON s.Id = r.SpecimenId
            """);

        yield return (ReportViewCatalog.SpecimenRejections, $"""
            SELECT
                {rejectedAt} AS rejected_utc,
                {rejectedMonth} AS rejected_month,
                COALESCE(s.RejectionReasonCode, 'UNCODED') AS rejection_reason_code,
                s.RejectionReason AS rejection_reason,
                s.RejectedBy AS rejected_by,
                s.AccessionNumber AS accession_number,
                s.SpecimenType AS specimen_type,
                COALESCE(s.DrawLocation, '') AS draw_location,
                COALESCE(s.Collector, '') AS collector,
                s.CollectedUtc AS collected_utc,
                s.ReceivedUtc AS received_utc,
                p.MedicalRecordNumber AS mrn,
                {person} AS patient_name
            FROM Specimens s
            INNER JOIN Patients p ON p.Id = s.PatientId
            WHERE s.Status = {(int)SpecimenStatus.Rejected}
            """);

        string Minutes(string from, string to) => sqlServer
            ? $"DATEDIFF(minute, {from}, {to})"
            : $"CAST(ROUND((julianday({to}) - julianday({from})) * 1440) AS INTEGER)";
        var verifiedMonth = sqlServer ? "CONVERT(varchar(7), r.VerifiedUtc, 126)" : "substr(r.VerifiedUtc, 1, 7)";
        yield return (ReportViewCatalog.TestTurnaround, $"""
            SELECT
                r.VerifiedUtc AS verified_utc,
                {verifiedMonth} AS verified_month,
                o.OrderedUtc AS ordered_utc,
                s.ReceivedUtc AS received_utc,
                {CaseEnum("o.Priority", typeof(OrderPriority))} AS priority,
                r.TestCode AS test_code,
                o.OrderNumber AS order_number,
                s.AccessionNumber AS accession_number,
                {Minutes("o.OrderedUtc", "r.VerifiedUtc")} AS minutes_order_to_verify,
                CASE WHEN s.ReceivedUtc IS NULL THEN NULL ELSE {Minutes("s.ReceivedUtc", "r.VerifiedUtc")} END AS minutes_receipt_to_verify,
                p.MedicalRecordNumber AS mrn,
                {person} AS patient_name
            FROM TestResults r
            INNER JOIN Orders o ON o.Id = r.OrderId
            INNER JOIN Specimens s ON s.Id = r.SpecimenId
            INNER JOIN Patients p ON p.Id = r.PatientId
            WHERE r.Status = {(int)ResultStatus.Verified} AND r.SupersededByResultId IS NULL AND r.VerifiedUtc IS NOT NULL
            """);

        // Blood orders: first reservation and first issue per order; a unit is ready at the earlier of the two.
        var orderedMonth = sqlServer ? "CONVERT(varchar(7), o.OrderedUtc, 126)" : "substr(o.OrderedUtc, 1, 7)";
        var firstReady = "CASE WHEN fr.FirstReservedUtc IS NULL THEN fi.FirstIssuedUtc WHEN fi.FirstIssuedUtc IS NULL OR fr.FirstReservedUtc <= fi.FirstIssuedUtc THEN fr.FirstReservedUtc ELSE fi.FirstIssuedUtc END";
        yield return (ReportViewCatalog.BloodOrderTurnaround, $"""
            SELECT
                o.OrderedUtc AS ordered_utc,
                {orderedMonth} AS ordered_month,
                {CaseEnum("o.Priority", typeof(OrderPriority))} AS priority,
                o.OrderNumber AS order_number,
                {CaseEnum("o.Status", typeof(OrderStatus))} AS status,
                pl.UnitsOrdered AS units_ordered,
                fr.FirstReservedUtc AS first_reserved_utc,
                fi.FirstIssuedUtc AS first_issued_utc,
                CASE WHEN fr.FirstReservedUtc IS NULL THEN NULL ELSE {Minutes("o.OrderedUtc", "fr.FirstReservedUtc")} END AS minutes_order_to_reserve,
                CASE WHEN fi.FirstIssuedUtc IS NULL THEN NULL ELSE {Minutes("o.OrderedUtc", "fi.FirstIssuedUtc")} END AS minutes_order_to_issue,
                o.NeededByUtc AS needed_by_utc,
                CASE WHEN o.NeededByUtc IS NULL OR ({firstReady}) IS NULL THEN NULL
                     WHEN ({firstReady}) <= o.NeededByUtc THEN '{BloodOrderTurnaroundRule.Met}' ELSE '{BloodOrderTurnaroundRule.Missed}' END AS ready_by_needed_by,
                p.MedicalRecordNumber AS mrn,
                {person} AS patient_name
            FROM Orders o
            INNER JOIN (SELECT OrderId, COUNT(*) AS UnitsOrdered FROM OrderLines WHERE IsActive = 1 AND LineCategory = {(int)OrderCategory.Product} GROUP BY OrderId) pl ON pl.OrderId = o.Id
            INNER JOIN Patients p ON p.Id = o.PatientId
            LEFT JOIN (SELECT OrderId, MIN(AllocatedUtc) AS FirstReservedUtc FROM Allocations WHERE OrderId IS NOT NULL GROUP BY OrderId) fr ON fr.OrderId = o.Id
            LEFT JOIN (SELECT OrderId, MIN(IssuedUtc) AS FirstIssuedUtc FROM Issues WHERE OrderId IS NOT NULL GROUP BY OrderId) fi ON fi.OrderId = o.Id
            WHERE o.Status NOT IN ({(int)OrderStatus.Cancelled}, {(int)OrderStatus.Discontinued})
            """);
    }

    private static string TextType(bool sqlServer) => sqlServer ? "nvarchar(80)" : "text";

    private static string CaseEnum(string column, Type enumType)
    {
        var whens = Enum.GetValues(enumType).Cast<Enum>()
            .Select(value => $"WHEN {Convert.ToInt32(value, CultureInfo.InvariantCulture)} THEN '{value}'");
        return $"CASE {column} {string.Join(' ', whens)} ELSE '' END";
    }
}
