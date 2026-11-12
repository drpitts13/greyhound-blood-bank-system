using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
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
            ReportSourceCatalog.LabResults => LabResultsAsync(cancellationToken),
            ReportSourceCatalog.SpecimenRejections => SpecimenRejectionsAsync(cancellationToken),
            ReportSourceCatalog.TestTurnaround => TestTurnaroundAsync(cancellationToken),
            ReportSourceCatalog.BloodOrderTurnaround => BloodOrderTurnaroundAsync(cancellationToken),
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
                Disposition = transfusion != null ? (TransfusionDisposition?)transfusion.FinalDisposition : null,
                Running = transfusion != null && transfusion.StopUtc == null && issue.Status == IssueStatus.Issued
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
            ("disposition", row.Running ? TransfusionRecordState.InProgress : row.Disposition?.ToString()))).ToList();
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
                unit.DiscardReasonCode,
                DiscardedUtc = _db.InventoryStatusHistory
                    .Where(h => h.BloodProductId == unit.Id && h.ToStatus == UnitStatus.Discarded)
                    .Max(h => (DateTime?)h.ChangedUtc),
                unit.ExpiresUtc
            }).ToListAsync(cancellationToken);

        return rows.Select(row => ReportValueFormat.Row(
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_name", ReportValueFormat.Text(row.ProductName)),
            ("abo", row.Abo.ToString()),
            ("rh", row.RhD.ToString()),
            ("status", row.Status.ToString()),
            ("discard_reason", ReportValueFormat.Text(row.DiscardReason)),
            ("discard_reason_code", ReportValueFormat.Text(row.DiscardReasonCode)),
            ("discarded_utc", row.DiscardedUtc is DateTime discarded ? ReportValueFormat.Date(discarded) : null),
            ("discarded_month", row.DiscardedUtc?.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)),
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
            join issue in _db.Issues.AsNoTracking() on transfusion.IssueId equals issue.Id
            select new
            {
                transfusion.StartUtc,
                unit.UnitNumber,
                ProductName = product.Name,
                patient.MedicalRecordNumber,
                transfusion.FinalDisposition,
                Running = transfusion.StopUtc == null && issue.Status == IssueStatus.Issued
            }).ToListAsync(cancellationToken);

        var rows = crossmatches.Select(row => ReportValueFormat.Row(
            ("occurred_utc", ReportValueFormat.Date(row.PerformedUtc)),
            ("occurred_month", row.PerformedUtc.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)),
            ("event_type", "Crossmatch"),
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_name", ReportValueFormat.Text(row.ProductName)),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
            ("detail", row.Method + " " + row.Result))).ToList();

        rows.AddRange(transfusions.Select(row => ReportValueFormat.Row(
            ("occurred_utc", ReportValueFormat.Date(row.StartUtc)),
            ("occurred_month", row.StartUtc?.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)),
            ("event_type", "Transfusion"),
            ("unit_number", ReportValueFormat.Text(row.UnitNumber)),
            ("product_name", ReportValueFormat.Text(row.ProductName)),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
            ("detail", row.Running ? TransfusionRecordState.InProgress : row.FinalDisposition.ToString()))));

        return rows;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> LabResultsAsync(CancellationToken cancellationToken)
    {
        var definitions = await _db.TestDefinitions.AsNoTracking()
            .Where(definition => definition.IsActive && !definition.IsDraft)
            .Select(definition => new { definition.Id, definition.Code, definition.Name, definition.Category })
            .ToListAsync(cancellationToken);
        var byCode = definitions
            .GroupBy(definition => definition.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(definition => definition.Id).First(),
                StringComparer.OrdinalIgnoreCase);

        var rows = await (
            from result in _db.TestResults.AsNoTracking()
            join patient in _db.Patients.AsNoTracking() on result.PatientId equals patient.Id
            join specimen in _db.Specimens.AsNoTracking() on result.SpecimenId equals specimen.Id
            select new
            {
                patient.MedicalRecordNumber,
                patient.LastName,
                patient.FirstName,
                patient.DateOfBirth,
                patient.Sex,
                PatientStatus = patient.Status,
                specimen.AccessionNumber,
                specimen.SpecimenType,
                SpecimenStatus = specimen.Status,
                specimen.CollectedUtc,
                specimen.ReceivedUtc,
                specimen.ExpiresUtc,
                result.TestCode,
                result.Value,
                result.Units,
                result.Interpretation,
                ResultStatus = result.Status,
                result.EnteredUtc,
                result.VerifiedUtc
            }).ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            byCode.TryGetValue(row.TestCode, out var definition);
            return ReportValueFormat.Row(
                ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
                ("patient_name", ReportValueFormat.Name(row.LastName, row.FirstName)),
                ("date_of_birth", ReportValueFormat.Date(row.DateOfBirth)),
                ("sex", row.Sex.ToString()),
                ("patient_status", row.PatientStatus.ToString()),
                ("accession_number", ReportValueFormat.Text(row.AccessionNumber)),
                ("specimen_type", ReportValueFormat.Text(row.SpecimenType)),
                ("specimen_status", row.SpecimenStatus.ToString()),
                ("collected_utc", ReportValueFormat.Date(row.CollectedUtc)),
                ("received_utc", ReportValueFormat.Date(row.ReceivedUtc)),
                ("expires_utc", ReportValueFormat.Date(row.ExpiresUtc)),
                ("test_code", ReportValueFormat.Text(row.TestCode)),
                ("test_name", ReportValueFormat.Text(definition?.Name)),
                ("test_category", definition is null ? null : definition.Category.ToString()),
                ("result_value", ReportValueFormat.Text(row.Value)),
                ("units", ReportValueFormat.Text(row.Units)),
                ("interpretation", ReportValueFormat.Text(row.Interpretation)),
                ("result_status", row.ResultStatus.ToString()),
                ("entered_utc", ReportValueFormat.Date(row.EnteredUtc)),
                ("verified_utc", ReportValueFormat.Date(row.VerifiedUtc)));
        }).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> TestTurnaroundAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from result in _db.TestResults.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on result.OrderId equals order.Id
            join specimen in _db.Specimens.AsNoTracking() on result.SpecimenId equals specimen.Id
            join patient in _db.Patients.AsNoTracking() on result.PatientId equals patient.Id
            where result.Status == ResultStatus.Verified && result.SupersededByResultId == null && result.VerifiedUtc != null
            select new
            {
                result.VerifiedUtc,
                order.OrderedUtc,
                specimen.ReceivedUtc,
                order.Priority,
                result.TestCode,
                order.OrderNumber,
                specimen.AccessionNumber,
                patient.MedicalRecordNumber,
                patient.LastName,
                patient.FirstName
            }).ToListAsync(cancellationToken);

        static string? Minutes(DateTime? from, DateTime? to) =>
            from is DateTime a && to is DateTime b
                ? Math.Round((b - a).TotalMinutes).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;

        return rows.Select(row => ReportValueFormat.Row(
            ("verified_utc", ReportValueFormat.Date(row.VerifiedUtc)),
            ("verified_month", row.VerifiedUtc?.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)),
            ("ordered_utc", ReportValueFormat.Date(row.OrderedUtc)),
            ("received_utc", ReportValueFormat.Date(row.ReceivedUtc)),
            ("priority", row.Priority.ToString()),
            ("test_code", ReportValueFormat.Text(row.TestCode)),
            ("order_number", ReportValueFormat.Text(row.OrderNumber)),
            ("accession_number", ReportValueFormat.Text(row.AccessionNumber)),
            ("minutes_order_to_verify", Minutes(row.OrderedUtc, row.VerifiedUtc)),
            ("minutes_receipt_to_verify", Minutes(row.ReceivedUtc, row.VerifiedUtc)),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
            ("patient_name", ReportValueFormat.Name(row.LastName, row.FirstName)))).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> BloodOrderTurnaroundAsync(CancellationToken cancellationToken)
    {
        var unitsOrdered = await _db.OrderLines.AsNoTracking()
            .Where(l => l.IsActive && l.LineCategory == OrderCategory.Product)
            .GroupBy(l => l.OrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.OrderId, g => g.Count, cancellationToken);
        if (unitsOrdered.Count == 0)
            return [];

        var orderIds = unitsOrdered.Keys.ToList();
        var rows = await (
            from order in _db.Orders.AsNoTracking()
            join patient in _db.Patients.AsNoTracking() on order.PatientId equals patient.Id
            where orderIds.Contains(order.Id) && order.Status != OrderStatus.Cancelled && order.Status != OrderStatus.Discontinued
            select new
            {
                order.Id,
                order.OrderedUtc,
                order.Priority,
                order.OrderNumber,
                order.Status,
                order.NeededByUtc,
                patient.MedicalRecordNumber,
                patient.LastName,
                patient.FirstName
            }).ToListAsync(cancellationToken);
        var reservedTimes = await _db.Allocations.AsNoTracking()
            .Where(a => a.OrderId != null && orderIds.Contains(a.OrderId.Value))
            .Select(a => new { OrderId = a.OrderId!.Value, a.AllocatedUtc })
            .ToListAsync(cancellationToken);
        var issuedTimes = await _db.Issues.AsNoTracking()
            .Where(i => i.OrderId != null && orderIds.Contains(i.OrderId.Value))
            .Select(i => new { OrderId = i.OrderId!.Value, i.IssuedUtc })
            .ToListAsync(cancellationToken);
        var firstReserved = reservedTimes.GroupBy(a => a.OrderId).ToDictionary(g => g.Key, g => g.Min(a => a.AllocatedUtc));
        var firstIssued = issuedTimes.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => g.Min(i => i.IssuedUtc));

        static string? Number(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return rows.Select(row =>
        {
            DateTime? reserved = firstReserved.TryGetValue(row.Id, out var r) ? r : null;
            DateTime? issued = firstIssued.TryGetValue(row.Id, out var i) ? i : null;
            var ready = BloodOrderTurnaroundRule.FirstReady(reserved, issued);
            return ReportValueFormat.Row(
                ("ordered_utc", ReportValueFormat.Date(row.OrderedUtc)),
                ("ordered_month", row.OrderedUtc.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)),
                ("priority", row.Priority.ToString()),
                ("order_number", ReportValueFormat.Text(row.OrderNumber)),
                ("status", row.Status.ToString()),
                ("units_ordered", Number(unitsOrdered[row.Id])),
                ("first_reserved_utc", ReportValueFormat.Date(reserved)),
                ("first_issued_utc", ReportValueFormat.Date(issued)),
                ("minutes_order_to_reserve", Number(BloodOrderTurnaroundRule.Minutes(row.OrderedUtc, reserved))),
                ("minutes_order_to_issue", Number(BloodOrderTurnaroundRule.Minutes(row.OrderedUtc, issued))),
                ("needed_by_utc", ReportValueFormat.Date(row.NeededByUtc)),
                ("ready_by_needed_by", BloodOrderTurnaroundRule.ReadyByNeededBy(ready, row.NeededByUtc)),
                ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
                ("patient_name", ReportValueFormat.Name(row.LastName, row.FirstName)));
        }).ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> SpecimenRejectionsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from specimen in _db.Specimens.AsNoTracking()
            join patient in _db.Patients.AsNoTracking() on specimen.PatientId equals patient.Id
            where specimen.Status == SpecimenStatus.Rejected
            select new
            {
                // Specimens rejected before RejectedUtc was recorded fall back to their last modification time.
                RejectedUtc = specimen.RejectedUtc ?? specimen.ModifiedUtc,
                specimen.RejectionReasonCode,
                specimen.RejectionReason,
                specimen.RejectedBy,
                specimen.AccessionNumber,
                specimen.SpecimenType,
                specimen.DrawLocation,
                specimen.Collector,
                specimen.CollectedUtc,
                specimen.ReceivedUtc,
                patient.MedicalRecordNumber,
                patient.LastName,
                patient.FirstName
            }).ToListAsync(cancellationToken);

        return rows.Select(row => ReportValueFormat.Row(
            ("rejected_utc", ReportValueFormat.Date(row.RejectedUtc)),
            ("rejected_month", row.RejectedUtc?.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)),
            ("rejection_reason_code", string.IsNullOrWhiteSpace(row.RejectionReasonCode) ? "UNCODED" : row.RejectionReasonCode),
            ("rejection_reason", ReportValueFormat.Text(row.RejectionReason)),
            ("rejected_by", ReportValueFormat.Text(row.RejectedBy)),
            ("accession_number", ReportValueFormat.Text(row.AccessionNumber)),
            ("specimen_type", ReportValueFormat.Text(row.SpecimenType)),
            ("draw_location", ReportValueFormat.Text(row.DrawLocation) ?? string.Empty),
            ("collector", ReportValueFormat.Text(row.Collector) ?? string.Empty),
            ("collected_utc", ReportValueFormat.Date(row.CollectedUtc)),
            ("received_utc", ReportValueFormat.Date(row.ReceivedUtc)),
            ("mrn", ReportValueFormat.Text(row.MedicalRecordNumber)),
            ("patient_name", ReportValueFormat.Name(row.LastName, row.FirstName)))).ToList();
    }
}
