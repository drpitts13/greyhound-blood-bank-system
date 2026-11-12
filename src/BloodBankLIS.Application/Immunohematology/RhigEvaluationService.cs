using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Immunohematology;

public sealed record RhigEvaluationDto(
    long Id,
    long PatientId,
    RhigContext Context,
    DateTime EventUtc,
    RhType InfantRh,
    FetalBleedScreen FetalBleedScreen,
    decimal? FetalBleedPercent,
    RhigDecision Decision,
    int? VialsRecommended,
    string? DecisionNote,
    string RecordedBy,
    DateTime RecordedUtc,
    string? CompletedBy,
    DateTime? CompletedUtc,
    IReadOnlyList<string> Facts,
    string? GivenLot = null,
    int? GivenVials = null,
    DateTime? GivenUtc = null,
    string? GivenBy = null,
    string? GivenNote = null,
    // OCD-069: what the facts on file suggest, and the vials calculated from the quantitation; the reviewer confirms.
    RhigDecision? SuggestedDecision = null,
    string? SuggestionReason = null,
    int? VialsCalculated = null,
    string? DoseExplanation = null)
{
    public bool IsCompleted => CompletedUtc is not null;

    /// <summary>The recorded decision differs from the suggestion (both known).</summary>
    public bool DecisionDiffersFromSuggestion => SuggestedDecision is { } s && Decision != RhigDecision.Undetermined && Decision != s;

    /// <summary>The vials recommended differ from those calculated (both known).</summary>
    public bool VialsDifferFromCalculated => VialsCalculated is int c && VialsRecommended is int r && c != r;

    public bool IsGiven => GivenUtc is not null;

    /// <summary>A completed candidate with no RhIG given recorded yet.</summary>
    public bool AwaitsGiving => IsCompleted && Decision == RhigDecision.Candidate && !IsGiven;

    public string? GivenDifference => RhigEvaluationRule.GivenDifference(VialsRecommended, GivenVials);
}

/// <summary>
/// An RhIG evaluation still needing work on the cross-patient queue: open (ready when it can be completed as recorded),
/// or completed as a candidate with no RhIG given recorded.
/// </summary>
public sealed record RhigQueueItemDto(RhigEvaluationDto Evaluation, string PatientName, string Mrn, bool ReadyToComplete)
{
    /// <summary>When RhIG is to be given by (OCD-087), and whether that has passed; null when not set or not due.</summary>
    public DateTime? GiveByUtc { get; init; }
    public bool PastGiveBy { get; init; }
}

/// <summary>A patient who was given an RhIG lot (recall trace).</summary>
public sealed record RhigLotRecipientDto(long PatientId, string PatientName, string Mrn, string Lot, int? Vials, DateTime GivenUtc, string? GivenBy, long EvaluationId);

/// <summary>The RhIG lot and vials issued for a completed candidate evaluation.</summary>
public sealed record RecordRhigGivenRequest(string Lot, int Vials, DateTime GivenUtc, string? Note);

/// <summary>Creates or updates an open RhIG evaluation (null id creates one).</summary>
public sealed record SaveRhigEvaluationRequest(
    long? Id,
    RhigContext Context,
    DateTime EventUtc,
    RhType InfantRh,
    FetalBleedScreen FetalBleedScreen,
    decimal? FetalBleedPercent,
    RhigDecision Decision,
    int? VialsRecommended,
    string? DecisionNote);

/// <summary>
/// RhIG evaluations on a patient's chart (OCD-069). Recording and editing need immuno.record; completing needs
/// result.verify. Each change is audited; a completed evaluation is not edited.
/// </summary>
public sealed class RhigEvaluationService
{
    private readonly IRepository<RhigEvaluation> _evaluations;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly BloodBankLIS.Application.Compliance.FacilityPolicyService? _policy;
    private readonly IRepository<RhigLot>? _lots;

    private readonly IFacilityTimeZone? _timeZone;

    public RhigEvaluationService(
        IRepository<RhigEvaluation> evaluations,
        IRepository<Patient> patients,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        BloodBankLIS.Application.Compliance.FacilityPolicyService? policy = null,
        IRepository<RhigLot>? lots = null,
        IFacilityTimeZone? timeZone = null)
    {
        _timeZone = timeZone;
        _lots = lots;
        _policy = policy;
        _evaluations = evaluations;
        _patients = patients;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<RhigEvaluationDto>> ListAsync(long patientId, CancellationToken ct = default)
    {
        var rows = await _evaluations.ListAsync(e => e.PatientId == patientId, ct);
        var (motherRh, antiD, variant) = await PatientFactsAsync(patientId, ct);
        var dose = await DoseSettingsAsync(ct);
        return rows
            .OrderByDescending(e => e.EventUtc)
            .ThenByDescending(e => e.Id)
            .Select(e => ToDto(e, motherRh, antiD, variant, dose))
            .ToList();
    }

    /// <summary>
    /// Every open evaluation, oldest event first, with the patient and whether it is filled in enough to complete (the
    /// same check as Complete; no clinical rule).
    /// </summary>
    public async Task<IReadOnlyList<RhigQueueItemDto>> ListOpenAsync(CancellationToken ct = default)
    {
        var open = await _evaluations.ListAsync(
            e => e.CompletedUtc == null || (e.Decision == RhigDecision.Candidate && e.GivenUtc == null), ct);
        if (open.Count == 0)
        {
            return [];
        }

        var patientIds = open.Select(e => e.PatientId).Distinct().ToList();
        var patients = (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var windowHours = _policy is null ? 0 : await _policy.GetRhigAdministrationWindowHoursAsync(ct);
        var now = _clock.UtcNow;
        var items = new List<RhigQueueItemDto>();
        foreach (var e in open.OrderBy(e => e.EventUtc).ThenBy(e => e.Id))
        {
            patients.TryGetValue(e.PatientId, out var patient);
            if (patient is { Status: PatientStatus.Merged })
            {
                continue;
            }

            var (motherRh, antiD, variant) = await PatientFactsAsync(e.PatientId, ct);
            var dose = await DoseSettingsAsync(ct);
            var ready = e.CompletedUtc is null
                && RhigEvaluationRule.EvaluateComplete(e.Decision, e.VialsRecommended, e.DecisionNote, e.FetalBleedScreen, e.FetalBleedPercent)
                    .Severity != RuleSeverity.HardStop;
            var giveBy = RhigEvaluationRule.GiveBy(e.EventUtc, windowHours, e.Decision, e.GivenUtc is not null);
            items.Add(new RhigQueueItemDto(
                ToDto(e, motherRh, antiD, variant, dose),
                patient is null ? $"Patient {e.PatientId}" : $"{patient.LastName}, {patient.FirstName}",
                patient?.MedicalRecordNumber ?? "—",
                ready)
            {
                GiveByUtc = giveBy,
                PastGiveBy = giveBy is DateTime by && now > by
            });
        }

        return items;
    }

    public async Task<OperationResult<RhigEvaluationDto>> SaveAsync(long patientId, SaveRhigEvaluationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await DeniedAsync(PermissionCodes.ImmunoRecord, "Recording an RhIG evaluation requires immunohematology record permission.", ct);
        if (denied is not null)
        {
            return denied;
        }

        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null || patient.Status == PatientStatus.Merged)
        {
            return OperationResult<RhigEvaluationDto>.Fail("Patient not found or merged into another chart.");
        }

        var values = RhigEvaluationRule.EvaluateValues(request.VialsRecommended, request.FetalBleedPercent);
        if (values.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<RhigEvaluationDto>.Fail(values.Message);
        }

        if (request.EventUtc > _clock.UtcNow.AddMinutes(5))
        {
            return OperationResult<RhigEvaluationDto>.Fail("The event time cannot be in the future.");
        }

        RhigEvaluation row;
        object? before = null;
        if (request.Id is long id)
        {
            var existing = await _evaluations.GetByIdAsync(id, ct);
            if (existing is null || existing.PatientId != patientId)
            {
                return OperationResult<RhigEvaluationDto>.Fail("RhIG evaluation not found.");
            }

            if (existing.CompletedUtc is not null)
            {
                return OperationResult<RhigEvaluationDto>.Fail("A completed RhIG evaluation cannot be changed; record a new one.");
            }

            before = Snapshot(existing);
            row = existing;
        }
        else
        {
            row = new RhigEvaluation
            {
                PatientId = patientId,
                RecordedBy = _currentUser.UserName,
                RecordedUtc = _clock.UtcNow
            };
            await _evaluations.AddAsync(row, ct);
        }

        row.Context = request.Context;
        row.EventUtc = DateTime.SpecifyKind(request.EventUtc, DateTimeKind.Utc);
        row.InfantRh = request.InfantRh;
        row.FetalBleedScreen = request.FetalBleedScreen;
        row.FetalBleedPercent = request.FetalBleedScreen == FetalBleedScreen.Positive ? request.FetalBleedPercent : null;
        row.Decision = request.Decision;
        row.VialsRecommended = request.Decision == RhigDecision.Candidate ? request.VialsRecommended : null;
        row.DecisionNote = string.IsNullOrWhiteSpace(request.DecisionNote) ? null : request.DecisionNote.Trim();
        if (before is not null)
        {
            _evaluations.Update(row);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(
            before is null ? AuditEventType.Create : AuditEventType.Update,
            nameof(RhigEvaluation),
            row.Id,
            oldValue: before,
            newValue: Snapshot(row),
            reason: before is null ? "RhIG evaluation recorded." : "RhIG evaluation updated.");
        await _unitOfWork.SaveChangesAsync(ct);
        return await ResultAsync(row, ct);
    }

    public async Task<OperationResult<RhigEvaluationDto>> CompleteAsync(long patientId, long id, CancellationToken ct = default)
    {
        var denied = await DeniedAsync(PermissionCodes.ResultVerify, "Completing an RhIG evaluation requires result verification permission.", ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _evaluations.GetByIdAsync(id, ct);
        if (row is null || row.PatientId != patientId)
        {
            return OperationResult<RhigEvaluationDto>.Fail("RhIG evaluation not found.");
        }

        if (row.CompletedUtc is not null)
        {
            return OperationResult<RhigEvaluationDto>.Fail("This RhIG evaluation is already completed.");
        }

        var complete = RhigEvaluationRule.EvaluateComplete(row.Decision, row.VialsRecommended, row.DecisionNote, row.FetalBleedScreen, row.FetalBleedPercent);
        if (complete.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<RhigEvaluationDto>.Fail(complete.Message);
        }

        // OCD-069: what the application suggested and calculated is kept with the decision the reviewer completed.
        var (patientRh, antiDOnHistory, patientVariant) = await PatientFactsAsync(row.PatientId, ct);
        row.SuggestedDecision = RhigDoseRule.Suggest(patientRh, antiDOnHistory, row.InfantRh, patientVariant).Suggested;
        row.VialsCalculated = RhigDoseRule.Calculate(row.FetalBleedScreen, row.FetalBleedPercent, await DoseSettingsAsync(ct)).Vials;
        row.CompletedBy = _currentUser.UserName;
        row.CompletedUtc = _clock.UtcNow;
        _evaluations.Update(row);
        _audit.Record(
            AuditEventType.Update,
            nameof(RhigEvaluation),
            row.Id,
            oldValue: new { CompletedUtc = (DateTime?)null },
            newValue: Snapshot(row),
            reason: "RhIG evaluation completed.");
        await _unitOfWork.SaveChangesAsync(ct);
        return await ResultAsync(row, ct);
    }

    /// <summary>
    /// Every patient given an RhIG lot, newest first, for a recall (exact lot, trimmed, case-insensitive). Merged charts
    /// are shown under the chart they were merged into.
    /// </summary>
    public async Task<IReadOnlyList<RhigLotRecipientDto>> FindByLotAsync(string? lot, CancellationToken ct = default)
    {
        var wanted = lot?.Trim();
        if (string.IsNullOrEmpty(wanted))
        {
            return [];
        }

        var upper = wanted.ToUpperInvariant();
        var given = await _evaluations.ListAsync(e => e.GivenLot != null && e.GivenLot.ToUpper() == upper, ct);
        if (given.Count == 0)
        {
            return [];
        }

        var ids = given.Select(e => e.PatientId).Distinct().ToList();
        var patients = (await _patients.ListAsync(p => ids.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var survivorIds = patients.Values.Where(p => p.MergedIntoPatientId is not null).Select(p => p.MergedIntoPatientId!.Value).Distinct().ToList();
        var survivors = survivorIds.Count == 0
            ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => survivorIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        return given
            .OrderByDescending(e => e.GivenUtc)
            .Select(e =>
            {
                patients.TryGetValue(e.PatientId, out var patient);
                if (patient?.MergedIntoPatientId is long survivorId && survivors.TryGetValue(survivorId, out var survivor))
                {
                    patient = survivor;
                }

                return new RhigLotRecipientDto(
                    patient?.Id ?? e.PatientId,
                    patient is null ? $"Patient {e.PatientId}" : $"{patient.LastName}, {patient.FirstName}",
                    patient?.MedicalRecordNumber ?? "—",
                    e.GivenLot!,
                    e.GivenVials,
                    e.GivenUtc!.Value,
                    e.GivenBy,
                    e.Id);
            })
            .ToList();
    }

    /// <summary>Records the RhIG lot and vials issued for a completed candidate evaluation (issue.create; audited; once).</summary>
    public async Task<OperationResult<RhigEvaluationDto>> RecordGivenAsync(long patientId, long id, RecordRhigGivenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await DeniedAsync(PermissionCodes.IssueCreate, "Recording RhIG given requires issue permission.", ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _evaluations.GetByIdAsync(id, ct);
        if (row is null || row.PatientId != patientId)
        {
            return OperationResult<RhigEvaluationDto>.Fail("RhIG evaluation not found.");
        }

        var givenUtc = DateTime.SpecifyKind(request.GivenUtc, DateTimeKind.Utc);
        var check = RhigEvaluationRule.EvaluateGiven(
            row.CompletedUtc is not null, row.Decision, row.GivenUtc is not null, request.Lot, request.Vials, givenUtc, _clock.UtcNow);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<RhigEvaluationDto>.Fail(check.Message);
        }

        // The lot register, once in use (end-user testing, 2026-11-12): the lot given must be registered, active and in date.
        string? lotWarning = null;
        if (_lots is not null)
        {
            var all = await _lots.ListAsync(ct);
            var upper = request.Lot.Trim().ToUpperInvariant();
            var match = all.FirstOrDefault(l => l.LotNumber.Trim().ToUpperInvariant() == upper);
            var used = match is null ? 0 : (await _evaluations.ListAsync(e => e.GivenLot != null && e.GivenLot.ToUpper() == upper, ct)).Sum(e => e.GivenVials ?? 0);
            var lotCheck = RhigLotRule.EvaluateUse(all.Count > 0, request.Lot,
                match is null ? null : (match.IsActive, match.ExpiresOn, match.VialsReceived), givenUtc, request.Vials, used,
                FacilityDay.Of(givenUtc, _timeZone));
            if (lotCheck.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<RhigEvaluationDto>.Fail(lotCheck.Message);
            }

            lotWarning = lotCheck.Severity == RuleSeverity.Warning ? lotCheck.Message : null;
            if (match is not null)
            {
                request = request with { Lot = match.LotNumber };
            }
        }

        row.GivenLot = request.Lot.Trim();
        row.GivenVials = request.Vials;
        row.GivenUtc = givenUtc;
        row.GivenBy = _currentUser.UserName;
        row.GivenNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        _evaluations.Update(row);
        _audit.Record(
            AuditEventType.Update,
            nameof(RhigEvaluation),
            row.Id,
            oldValue: new { GivenUtc = (DateTime?)null },
            newValue: new { row.PatientId, row.GivenLot, row.GivenVials, row.GivenUtc, row.GivenBy, row.GivenNote, row.VialsRecommended },
            reason: (RhigEvaluationRule.GivenDifference(row.VialsRecommended, row.GivenVials) is { } difference
                ? $"RhIG given recorded. {difference}"
                : "RhIG given recorded.") + (lotWarning is null ? "" : $" {lotWarning}"));
        await _unitOfWork.SaveChangesAsync(ct);
        return await ResultAsync(row, ct);
    }

    private async Task<OperationResult<RhigEvaluationDto>> ResultAsync(RhigEvaluation row, CancellationToken ct)
    {
        var (motherRh, antiD, variant) = await PatientFactsAsync(row.PatientId, ct);
        return OperationResult<RhigEvaluationDto>.Ok(ToDto(row, motherRh, antiD, variant, await DoseSettingsAsync(ct)));
    }

    private async Task<RhigDoseRule.Settings> DoseSettingsAsync(CancellationToken ct) =>
        _policy is null ? new RhigDoseRule.Settings(0, 0, RhigDoseRule.Rounding.NotSet) : await _policy.GetRhigDoseSettingsAsync(ct);

    private async Task<(RhType? MotherRh, bool AntiD, RhDVariant Variant)> PatientFactsAsync(long patientId, CancellationToken ct)
    {
        var current = await _bloodTypes.FirstOrDefaultAsync(b => b.PatientId == patientId && b.IsCurrent, ct);
        var antibodies = await _antibodies.ListAsync(a => a.PatientId == patientId, ct);
        var antiD = antibodies.Any(a => string.Equals(a.AntibodySpecificity?.Trim(), "anti-D", StringComparison.OrdinalIgnoreCase));
        var variant = (await _patients.GetByIdAsync(patientId, ct))?.RhDVariant ?? RhDVariant.None;
        return (current?.RhD, antiD, variant);
    }

    /// <summary>
    /// An open evaluation shows what the facts suggest now; a completed one shows what was suggested and calculated when
    /// it was completed, so the record does not change when the settings or chart later do.
    /// </summary>
    private static RhigEvaluationDto ToDto(RhigEvaluation e, RhType? motherRh, bool antiD, RhDVariant variant, RhigDoseRule.Settings dose)
    {
        var (suggested, reason) = RhigDoseRule.Suggest(motherRh, antiD, e.InfantRh, variant);
        var (vials, explanation) = RhigDoseRule.Calculate(e.FetalBleedScreen, e.FetalBleedPercent, dose);
        if (e.CompletedUtc is not null)
        {
            suggested = e.SuggestedDecision;
            vials = e.VialsCalculated;
        }

        return new(e.Id, e.PatientId, e.Context, e.EventUtc, e.InfantRh, e.FetalBleedScreen, e.FetalBleedPercent, e.Decision,
            e.VialsRecommended, e.DecisionNote, e.RecordedBy, e.RecordedUtc, e.CompletedBy, e.CompletedUtc,
            RhigEvaluationRule.Facts(motherRh, antiD, e.InfantRh, e.FetalBleedScreen, e.FetalBleedPercent, variant),
            e.GivenLot, e.GivenVials, e.GivenUtc, e.GivenBy, e.GivenNote,
            suggested, e.CompletedUtc is null ? reason : null, vials, e.CompletedUtc is null ? explanation : null);
    }

    private static object Snapshot(RhigEvaluation e) => new
    {
        e.PatientId,
        e.Context,
        e.EventUtc,
        e.InfantRh,
        e.FetalBleedScreen,
        e.FetalBleedPercent,
        e.Decision,
        e.VialsRecommended,
        e.DecisionNote,
        e.CompletedBy,
        e.CompletedUtc,
        e.SuggestedDecision,
        e.VialsCalculated
    };

    private async Task<OperationResult<RhigEvaluationDto>?> DeniedAsync(string permission, string message, CancellationToken ct) =>
        _permissions is not null && !await _permissions.HasPermissionAsync(_currentUser.UserName, permission, ct)
            ? OperationResult<RhigEvaluationDto>.Fail(message)
            : null;
}
