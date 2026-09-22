using System.Globalization;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;

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
                {CaseEnum("t.FinalDisposition", typeof(TransfusionDisposition))} AS disposition
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
                u.ExpiresUtc AS expires_utc
            FROM BloodProducts u
            INNER JOIN ProductTypes pt ON pt.Id = u.ProductTypeId
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
                'Transfusion' AS event_type,
                u.UnitNumber AS unit_number,
                pt.Name AS product_name,
                p.MedicalRecordNumber AS mrn,
                {CaseEnum("t.FinalDisposition", typeof(TransfusionDisposition))} AS detail
            FROM TransfusionEvents t
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
    }

    private static string TextType(bool sqlServer) => sqlServer ? "nvarchar(80)" : "text";

    private static string CaseEnum(string column, Type enumType)
    {
        var whens = Enum.GetValues(enumType).Cast<Enum>()
            .Select(value => $"WHEN {Convert.ToInt32(value, CultureInfo.InvariantCulture)} THEN '{value}'");
        return $"CASE {column} {string.Join(' ', whens)} ELSE '' END";
    }
}
