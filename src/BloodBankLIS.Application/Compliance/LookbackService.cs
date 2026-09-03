using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Compliance;

public sealed record LookbackUnitDto(
    long BloodProductId,
    string? Din,
    string UnitNumber,
    UnitStatus Status,
    bool IsDerivedComponent);

public sealed record LookbackRecipientDto(
    long BloodProductId,
    long? PatientId,
    long? IssueId,
    long? TransfusionEventId,
    string? IssuedToLocation,
    DateTime? IssuedUtc,
    string? MedicalRecordNumber = null,
    string? PatientName = null,
    string? UnitNumber = null);

public sealed record RecipientTracePatientDto(
    long PatientId,
    string MedicalRecordNumber,
    string LastName,
    string FirstName,
    DateOnly DateOfBirth,
    bool ResolvedFromMerge);

public sealed record RecipientTraceUnitDto(
    long BloodProductId,
    string UnitNumber,
    string? Din,
    UnitStatus Status,
    long IssueId,
    DateTime IssuedUtc,
    string? IssuedToLocation,
    IssueType IssueType,
    IssueStatus IssueStatus,
    long? TransfusionEventId,
    TransfusionDisposition? TransfusionDisposition,
    bool ReactionSuspected);

public sealed record RelatedComponentDto(
    string Din,
    long BloodProductId,
    string UnitNumber,
    UnitStatus Status,
    bool IssuedToIndexPatient);

public sealed record CoRecipientDto(
    string Din,
    long BloodProductId,
    string UnitNumber,
    long PatientId,
    string MedicalRecordNumber,
    string PatientName,
    long IssueId,
    DateTime IssuedUtc);

public sealed record RecipientTraceReportDto(
    RecipientTracePatientDto Patient,
    IReadOnlyList<RecipientTraceUnitDto> Units,
    IReadOnlyList<RelatedComponentDto> RelatedComponents,
    IReadOnlyList<CoRecipientDto> CoRecipients);

public sealed record LookbackReportDto(
    string Din,
    IReadOnlyList<LookbackUnitDto> Units,
    IReadOnlyList<LookbackRecipientDto> Recipients,
    IReadOnlyList<LookbackNotificationDto> Notifications);

public sealed record LookbackNotificationDto(
    long Id,
    string Din,
    long BloodProductId,
    long? PatientId,
    long? IssueId,
    LookbackNotificationStatus Status,
    string? PhysicianOfRecord,
    DateTime? AttemptedUtc,
    string? AttemptedBy,
    string? Notes)
{
    public static LookbackNotificationDto From(LookbackNotification n) => new(
        n.Id, n.Din, n.BloodProductId, n.PatientId, n.IssueId, n.Status,
        n.PhysicianOfRecord, n.AttemptedUtc, n.AttemptedBy, n.Notes);
}

public sealed record RecordLookbackAttemptRequest(string? PhysicianOfRecord, string? Notes, LookbackNotificationStatus Status);

public sealed class LookbackService
{
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<UnitModificationUnit> _modificationUnits;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<LookbackNotification> _notifications;
    private readonly InventoryService _inventoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    public LookbackService(
        IInventoryRepository inventory,
        IRepository<BloodUnit> units,
        IRepository<UnitModificationUnit> modificationUnits,
        IRepository<Issue> issues,
        IRepository<TransfusionEvent> transfusions,
        IRepository<Patient> patients,
        IRepository<LookbackNotification> notifications,
        InventoryService inventoryService,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _inventory = inventory;
        _units = units;
        _modificationUnits = modificationUnits;
        _issues = issues;
        _transfusions = transfusions;
        _patients = patients;
        _notifications = notifications;
        _inventoryService = inventoryService;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<OperationResult<LookbackReportDto>> FindByDinAsync(string din, CancellationToken ct = default)
    {
        var normalized = NormalizeDin(din);
        if (normalized.Length != 13)
        {
            return OperationResult<LookbackReportDto>.Fail("DIN must be 13 characters.");
        }

        var units = await CollectUnitsForDinAsync(normalized, ct);
        var recipients = new List<LookbackRecipientDto>();
        foreach (var unit in units)
        {
            var issues = await _issues.ListAsync(i => i.BloodProductId == unit.Id, ct);
            foreach (var issue in issues)
            {
                var tx = await _transfusions.FirstOrDefaultAsync(t => t.IssueId == issue.Id, ct);
                var recipient = await _patients.GetByIdAsync(issue.PatientId, ct);
                recipients.Add(new LookbackRecipientDto(
                    unit.Id, issue.PatientId, issue.Id, tx?.Id, issue.IssuedToLocation, issue.IssuedUtc,
                    recipient?.MedicalRecordNumber, FormatName(recipient), unit.UnitNumber));
            }
        }

        var notes = await _notifications.ListAsync(n => n.Din == normalized, ct);
        _audit.Record(AuditEventType.Lookback, nameof(BloodUnit), units.FirstOrDefault()?.Id, newValue: new { Din = normalized, UnitCount = units.Count }, reason: "Lookback search");
        await _unitOfWork.SaveChangesAsync(ct);

        return OperationResult<LookbackReportDto>.Ok(new LookbackReportDto(
            normalized,
            units.Select(u => new LookbackUnitDto(u.Id, u.Din, u.UnitNumber, u.Status, u.Din != normalized)).ToList(),
            recipients,
            notes.Select(LookbackNotificationDto.From).ToList()));
    }

    public async Task<OperationResult<LookbackReportDto>> RecallByDinAsync(string din, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<LookbackReportDto>.Fail("A reason is required to recall by DIN.");
        }

        var find = await FindByDinAsync(din, ct);
        if (!find.Succeeded || find.Value is null)
        {
            return find;
        }

        var normalized = find.Value.Din;
        var units = await CollectUnitsForDinAsync(normalized, ct);

        foreach (var unit in units)
        {
            if (unit.Status is UnitStatus.Transfused or UnitStatus.TransfusionStarted or UnitStatus.TransfusionStopped)
            {
                var issue = await _issues.FirstOrDefaultAsync(i => i.BloodProductId == unit.Id, ct);
                await _notifications.AddAsync(new LookbackNotification
                {
                    Din = normalized,
                    BloodProductId = unit.Id,
                    PatientId = issue?.PatientId,
                    IssueId = issue?.Id,
                    Status = LookbackNotificationStatus.Pending,
                    Reason = reason.Trim()
                }, ct);
                continue;
            }

            if (unit.Status != UnitStatus.Recalled && unit.Status != UnitStatus.Discarded)
            {
                await _inventoryService.RecallAsync(unit.Id, reason, ct);
            }
        }

        _audit.Record(AuditEventType.Lookback, nameof(BloodUnit), null, newValue: new { Din = normalized }, reason: reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return await FindByDinAsync(normalized, ct);
    }

    public async Task<OperationResult<LookbackNotification>> RecordAttemptAsync(
        long notificationId, RecordLookbackAttemptRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = await _notifications.GetByIdAsync(notificationId, ct);
        if (row is null)
        {
            return OperationResult<LookbackNotification>.Fail("Lookback notification not found.");
        }

        row.Status = request.Status;
        row.PhysicianOfRecord = request.PhysicianOfRecord;
        row.Notes = request.Notes;
        row.AttemptedUtc = _clock.UtcNow;
        row.AttemptedBy = _currentUser.UserName;
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<LookbackNotification>.Ok(row);
    }

    /// <summary>
    /// Recipient traceback: every unit issued to a patient, plus other components
    /// and co-recipients that share those donation identification numbers
    /// (21 CFR 606.165 bidirectional traceability).
    /// </summary>
    public async Task<OperationResult<RecipientTraceReportDto>> FindByRecipientAsync(
        string? mrn, long? patientId, CancellationToken ct = default)
    {
        var resolved = await ResolveRecipientAsync(mrn, patientId, ct);
        if (!resolved.Succeeded)
        {
            return OperationResult<RecipientTraceReportDto>.Fail(resolved.Error!);
        }

        var (patient, fromMerge) = resolved.Value;
        var patientIds = await CollectRelatedPatientIdsAsync(patient.Id, ct);
        var issues = (await _issues.ListAsync(i => patientIds.Contains(i.PatientId), ct))
            .OrderByDescending(i => i.IssuedUtc)
            .ToList();

        var units = new List<RecipientTraceUnitDto>();
        var issuedUnitIds = new HashSet<long>();
        var dins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issue in issues)
        {
            var unit = await _units.GetByIdAsync(issue.BloodProductId, ct)
                ?? await _inventory.GetUnitAsync(issue.BloodProductId, ct);
            if (unit is null)
            {
                continue;
            }

            issuedUnitIds.Add(unit.Id);
            var din = NormalizeDin(unit.Din ?? string.Empty);
            if (din.Length == 13)
            {
                dins.Add(din);
            }

            var tx = await _transfusions.FirstOrDefaultAsync(t => t.IssueId == issue.Id, ct);
            units.Add(new RecipientTraceUnitDto(
                unit.Id,
                unit.UnitNumber,
                unit.Din,
                unit.Status,
                issue.Id,
                issue.IssuedUtc,
                issue.IssuedToLocation,
                issue.IssueType,
                issue.Status,
                tx?.Id,
                tx?.FinalDisposition,
                tx?.ReactionSuspected ?? false));
        }

        var related = new List<RelatedComponentDto>();
        var coRecipients = new List<CoRecipientDto>();
        foreach (var din in dins)
        {
            var family = await CollectUnitsForDinAsync(din, ct);
            foreach (var unit in family)
            {
                var issuedToIndex = issuedUnitIds.Contains(unit.Id);
                if (!issuedToIndex)
                {
                    related.Add(new RelatedComponentDto(
                        din, unit.Id, unit.UnitNumber, unit.Status, IssuedToIndexPatient: false));
                }

                var otherIssues = await _issues.ListAsync(i => i.BloodProductId == unit.Id, ct);
                foreach (var other in otherIssues)
                {
                    if (patientIds.Contains(other.PatientId))
                    {
                        continue;
                    }

                    var otherPatient = await _patients.GetByIdAsync(other.PatientId, ct);
                    if (otherPatient is null)
                    {
                        continue;
                    }

                    coRecipients.Add(new CoRecipientDto(
                        din, unit.Id, unit.UnitNumber, otherPatient.Id,
                        otherPatient.MedicalRecordNumber, FormatName(otherPatient) ?? otherPatient.MedicalRecordNumber,
                        other.Id, other.IssuedUtc));
                }
            }
        }

        _audit.Record(
            AuditEventType.Lookback,
            nameof(Patient),
            patient.Id,
            newValue: new { patient.MedicalRecordNumber, UnitCount = units.Count, DonationCount = dins.Count },
            reason: "Recipient traceback");
        await _unitOfWork.SaveChangesAsync(ct);

        return OperationResult<RecipientTraceReportDto>.Ok(new RecipientTraceReportDto(
            new RecipientTracePatientDto(
                patient.Id, patient.MedicalRecordNumber, patient.LastName, patient.FirstName,
                patient.DateOfBirth, fromMerge),
            units,
            related,
            coRecipients));
    }

    private async Task<OperationResult<(Patient Patient, bool ResolvedFromMerge)>> ResolveRecipientAsync(
        string? mrn, long? patientId, CancellationToken ct)
    {
        if (patientId is null or <= 0 && string.IsNullOrWhiteSpace(mrn))
        {
            return OperationResult<(Patient, bool)>.Fail("MRN or patient id is required.");
        }

        Patient? patient = null;
        if (patientId is > 0)
        {
            patient = await _patients.GetByIdAsync(patientId.Value, ct);
        }

        if (patient is null && !string.IsNullOrWhiteSpace(mrn))
        {
            var trimmed = mrn.Trim();
            patient = await _patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == trimmed, ct);
            if (patient is null)
            {
                var upper = trimmed.ToUpperInvariant();
                var matches = await _patients.ListAsync(
                    p => p.MedicalRecordNumber.ToUpper() == upper, ct);
                patient = matches.FirstOrDefault();
            }
        }

        if (patient is null)
        {
            return OperationResult<(Patient, bool)>.Fail("Recipient not found.");
        }

        var fromMerge = false;
        var seen = new HashSet<long>();
        while (patient.MergedIntoPatientId is long survivorId && seen.Add(patient.Id))
        {
            var survivor = await _patients.GetByIdAsync(survivorId, ct);
            if (survivor is null)
            {
                break;
            }

            patient = survivor;
            fromMerge = true;
        }

        return OperationResult<(Patient, bool)>.Ok((patient, fromMerge));
    }

    private async Task<List<long>> CollectRelatedPatientIdsAsync(long survivingPatientId, CancellationToken ct)
    {
        var ids = new HashSet<long> { survivingPatientId };
        var merged = await _patients.ListAsync(p => p.MergedIntoPatientId == survivingPatientId, ct);
        foreach (var prior in merged)
        {
            ids.Add(prior.Id);
        }

        return ids.ToList();
    }

    private static string? FormatName(Patient? patient)
    {
        if (patient is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(patient.MiddleName)
            ? $"{patient.LastName}, {patient.FirstName}"
            : $"{patient.LastName}, {patient.FirstName} {patient.MiddleName}";
    }

    private async Task<List<BloodUnit>> CollectUnitsForDinAsync(string din13, CancellationToken ct)
    {
        var direct = (await _units.ListAsync(u => u.Din == din13, ct)).ToList();
        var found = new Dictionary<long, BloodUnit>();
        foreach (var unit in direct)
        {
            found[unit.Id] = unit;
        }

        foreach (var source in direct)
        {
            var sourceLinks = await _modificationUnits.ListAsync(
                l => l.BloodProductId == source.Id && l.Role == ModificationUnitRole.Source, ct);
            foreach (var link in sourceLinks)
            {
                var results = await _modificationUnits.ListAsync(
                    l => l.UnitModificationId == link.UnitModificationId && l.Role == ModificationUnitRole.Result, ct);
                foreach (var result in results)
                {
                    var child = await _inventory.GetUnitAsync(result.BloodProductId, ct);
                    if (child is not null)
                    {
                        found[child.Id] = child;
                    }
                }
            }
        }

        return found.Values.ToList();
    }

    private static string NormalizeDin(string din) =>
        new string((din ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
