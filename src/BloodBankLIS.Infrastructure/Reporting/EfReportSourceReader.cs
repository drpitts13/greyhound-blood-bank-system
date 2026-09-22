using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Reporting;

public sealed class EfReportSourceReader : IReportSourceReader
{
    private readonly BloodBankDbContext _db;

    public EfReportSourceReader(BloodBankDbContext db) => _db = db;

    public Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> LoadAsync(string sourceCode, CancellationToken cancellationToken = default) =>
        sourceCode.ToLowerInvariant() switch
        {
            ReportSourceCatalog.InventoryOnHand => InventoryAsync(cancellationToken),
            ReportSourceCatalog.IssueTransfusion => IssuesAsync(cancellationToken),
            ReportSourceCatalog.Discards => DiscardsAsync(cancellationToken),
            ReportSourceCatalog.Reactions => ReactionsAsync(cancellationToken),
            ReportSourceCatalog.Lookback => LookbackAsync(cancellationToken),
            ReportSourceCatalog.Deviations => DeviationsAsync(cancellationToken),
            ReportSourceCatalog.Overrides => OverridesAsync(cancellationToken),
            ReportSourceCatalog.CrossmatchTransfusion => ActivityAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unknown report source '{sourceCode}'.")
        };

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> InventoryAsync(CancellationToken cancellationToken)
    {
        var onHand = ReportInventoryScope.OnHand;
        var rows = await (
            from unit in _db.BloodUnits.AsNoTracking()
            join product in _db.ProductTypes.AsNoTracking() on unit.ProductTypeId equals product.Id
            join location in _db.InventoryLocations.AsNoTracking() on unit.CurrentLocationId equals location.Id into locations
            from location in locations.DefaultIfEmpty()
            where onHand.Contains(unit.Status)
            select new
            {
                unit.UnitNumber,
                product.ProductCode,
                ProductName = product.Name,
                unit.Abo,
                unit.RhD,
                unit.Status,
                Location = location != null ? location.Name : null,
                unit.ExpiresUtc
            }).ToListAsync(cancellationToken);

        return rows.Select(row => ReportValueFormat.Row(
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_code", ReportValueFormat.Text(row.ProductCode)),
            ("product_name", ReportValueFormat.Text(row.ProductName)),
            ("abo", row.Abo.ToString()),
            ("rh", row.RhD.ToString()),
            ("status", row.Status.ToString()),
            ("location", ReportValueFormat.Text(row.Location)),
            ("expires_utc", ReportValueFormat.Date(row.ExpiresUtc)))).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> IssuesAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from issue in _db.Issues.AsNoTracking()
            join unit in _db.BloodUnits.AsNoTracking() on issue.BloodProductId equals unit.Id
            join product in _db.ProductTypes.AsNoTracking() on unit.ProductTypeId equals product.Id
            join patient in _db.Patients.AsNoTracking() on issue.PatientId equals patient.Id
            join transfusion in _db.TransfusionEvents.AsNoTracking() on issue.Id equals transfusion.IssueId into transfusions
            from transfusion in transfusions.DefaultIfEmpty()
            select new
            {
                issue.IssuedUtc,
                unit.UnitNumber,
                ProductName = product.Name,
                unit.Abo,
                unit.RhD,
                patient.MedicalRecordNumber,
                patient.LastName,
                patient.FirstName,
                issue.IssueType,
                issue.Status,
                StartUtc = transfusion != null ? transfusion.StartUtc : null,
                StopUtc = transfusion != null ? transfusion.StopUtc : null,
                Disposition = transfusion != null ? (TransfusionDisposition?)transfusion.FinalDisposition : null
            }).ToListAsync(cancellationToken);

        return rows.Select(row => ReportValueFormat.Row(
            ("issued_utc", ReportValueFormat.Date(row.IssuedUtc)),
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_name", ReportValueFormat.Text(row.ProductName)),
            ("abo", row.Abo.ToString()),
            ("rh", row.RhD.ToString()),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
            ("patient_name", ReportValueFormat.Name(row.LastName, row.FirstName)),
            ("issue_type", row.IssueType.ToString()),
            ("issue_status", row.Status.ToString()),
            ("transfusion_start_utc", ReportValueFormat.Date(row.StartUtc)),
            ("transfusion_stop_utc", ReportValueFormat.Date(row.StopUtc)),
            ("disposition", row.Disposition?.ToString()))).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> DiscardsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from unit in _db.BloodUnits.AsNoTracking()
            join product in _db.ProductTypes.AsNoTracking() on unit.ProductTypeId equals product.Id
            where unit.Status == UnitStatus.Discarded || unit.DiscardReason != null
            select new
            {
                unit.UnitNumber,
                ProductName = product.Name,
                unit.Abo,
                unit.RhD,
                unit.Status,
                unit.DiscardReason,
                unit.ExpiresUtc
            }).ToListAsync(cancellationToken);

        return rows.Select(row => ReportValueFormat.Row(
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_name", ReportValueFormat.Text(row.ProductName)),
            ("abo", row.Abo.ToString()),
            ("rh", row.RhD.ToString()),
            ("status", row.Status.ToString()),
            ("discard_reason", ReportValueFormat.Text(row.DiscardReason)),
            ("expires_utc", ReportValueFormat.Date(row.ExpiresUtc)))).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ReactionsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from reaction in _db.ReactionInvestigations.AsNoTracking()
            join patient in _db.Patients.AsNoTracking() on reaction.PatientId equals patient.Id
            join unit in _db.BloodUnits.AsNoTracking() on reaction.BloodProductId equals unit.Id
            select new
            {
                reaction.ReportedUtc,
                reaction.Status,
                reaction.Severity,
                reaction.ReactionType,
                patient.MedicalRecordNumber,
                unit.UnitNumber,
                reaction.ProductAtFault,
                reaction.IsFatality
            }).ToListAsync(cancellationToken);

        return rows.Select(row => ReportValueFormat.Row(
            ("reported_utc", ReportValueFormat.Date(row.ReportedUtc)),
            ("status", row.Status.ToString()),
            ("severity", row.Severity.ToString()),
            ("reaction_type", ReportValueFormat.Text(row.ReactionType)),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_at_fault", ReportValueFormat.YesNo(row.ProductAtFault)),
            ("is_fatality", ReportValueFormat.YesNo(row.IsFatality)))).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> LookbackAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from note in _db.LookbackNotifications.AsNoTracking()
            join patient in _db.Patients.AsNoTracking() on note.PatientId equals patient.Id into patients
            from patient in patients.DefaultIfEmpty()
            select new
            {
                note.Din,
                note.Status,
                note.PhysicianOfRecord,
                note.Reason,
                note.AttemptedUtc,
                Mrn = patient != null ? patient.MedicalRecordNumber : null
            }).ToListAsync(cancellationToken);

        return rows.Select(row => ReportValueFormat.Row(
            ("din", ReportValueFormat.Text(row.Din)),
            ("status", row.Status.ToString()),
            ("physician", ReportValueFormat.Text(row.PhysicianOfRecord)),
            ("reason", ReportValueFormat.Text(row.Reason)),
            ("attempted_utc", ReportValueFormat.Date(row.AttemptedUtc)),
            ("mrn", ReportValueFormat.Text(row.Mrn)))).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> DeviationsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Deviations.AsNoTracking()
            .Where(row => row.Status != DeviationStatus.Closed)
            .Select(row => new
            {
                row.ReportedUtc,
                row.Title,
                row.Severity,
                row.Status,
                row.ReportedBy,
                row.ContextType
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => ReportValueFormat.Row(
            ("reported_utc", ReportValueFormat.Date(row.ReportedUtc)),
            ("title", ReportValueFormat.Text(row.Title)),
            ("severity", row.Severity.ToString()),
            ("status", row.Status.ToString()),
            ("reported_by", ReportValueFormat.Text(row.ReportedBy)),
            ("context_type", ReportValueFormat.Text(row.ContextType)))).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> OverridesAsync(CancellationToken cancellationToken)
    {
        var overrides = await _db.Overrides.AsNoTracking()
            .Select(row => new
            {
                row.OverriddenUtc,
                row.Action,
                row.RuleCode,
                row.Reason,
                row.AuthorizedBy
            })
            .ToListAsync(cancellationToken);

        var emergencies = await (
            from issue in _db.Issues.AsNoTracking()
            join unit in _db.BloodUnits.AsNoTracking() on issue.BloodProductId equals unit.Id
            join patient in _db.Patients.AsNoTracking() on issue.PatientId equals patient.Id
            where issue.IssueType == IssueType.EmergencyRelease || issue.IssueType == IssueType.MassiveTransfusion
            select new
            {
                issue.IssuedUtc,
                issue.IssueType,
                issue.EmergencyReleaseDetails,
                issue.IssuedBy,
                unit.UnitNumber,
                patient.MedicalRecordNumber
            }).ToListAsync(cancellationToken);

        var rows = overrides.Select(row => ReportValueFormat.Row(
            ("occurred_utc", ReportValueFormat.Date(row.OverriddenUtc)),
            ("kind", row.Action.ToString()),
            ("rule_code", ReportValueFormat.Text(row.RuleCode)),
            ("reason", ReportValueFormat.Text(row.Reason)),
            ("actor", ReportValueFormat.Text(row.AuthorizedBy)),
            ("unit_number", null),
            ("mrn", null))).ToList();

        rows.AddRange(emergencies.Select(row => ReportValueFormat.Row(
            ("occurred_utc", ReportValueFormat.Date(row.IssuedUtc)),
            ("kind", row.IssueType.ToString()),
            ("rule_code", row.IssueType.ToString()),
            ("reason", ReportValueFormat.Text(row.EmergencyReleaseDetails)),
            ("actor", ReportValueFormat.Text(row.IssuedBy)),
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)))));

        return rows;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ActivityAsync(CancellationToken cancellationToken)
    {
        var crossmatches = await (
            from crossmatch in _db.Crossmatches.AsNoTracking()
            join unit in _db.BloodUnits.AsNoTracking() on crossmatch.BloodProductId equals unit.Id
            join product in _db.ProductTypes.AsNoTracking() on unit.ProductTypeId equals product.Id
            join patient in _db.Patients.AsNoTracking() on crossmatch.PatientId equals patient.Id
            select new
            {
                crossmatch.PerformedUtc,
                unit.UnitNumber,
                ProductName = product.Name,
                patient.MedicalRecordNumber,
                crossmatch.Method,
                crossmatch.Result
            }).ToListAsync(cancellationToken);

        var transfusions = await (
            from transfusion in _db.TransfusionEvents.AsNoTracking()
            join unit in _db.BloodUnits.AsNoTracking() on transfusion.BloodProductId equals unit.Id
            join product in _db.ProductTypes.AsNoTracking() on unit.ProductTypeId equals product.Id
            join patient in _db.Patients.AsNoTracking() on transfusion.PatientId equals patient.Id
            select new
            {
                transfusion.StartUtc,
                unit.UnitNumber,
                ProductName = product.Name,
                patient.MedicalRecordNumber,
                transfusion.FinalDisposition
            }).ToListAsync(cancellationToken);

        var rows = crossmatches.Select(row => ReportValueFormat.Row(
            ("occurred_utc", ReportValueFormat.Date(row.PerformedUtc)),
            ("event_type", "Crossmatch"),
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_name", ReportValueFormat.Text(row.ProductName)),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
            ("detail", row.Method + " " + row.Result))).ToList();

        rows.AddRange(transfusions.Select(row => ReportValueFormat.Row(
            ("occurred_utc", ReportValueFormat.Date(row.StartUtc)),
            ("event_type", "Transfusion"),
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_name", ReportValueFormat.Text(row.ProductName)),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
            ("detail", row.FinalDisposition.ToString()))));

        return rows;
    }
}
