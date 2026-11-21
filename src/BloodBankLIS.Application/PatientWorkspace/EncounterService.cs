using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.PatientWorkspace;

namespace BloodBankLIS.Application.PatientWorkspace;

public sealed class EncounterService
{
    private readonly IRepository<Encounter> _encounters;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<OrderingProvider> _providers;
    private readonly OrderingProviderService _orderingProviders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IPermissionEvaluator? _permissions;
    private readonly ICurrentUser? _currentUser;
    private readonly IAuditWriter? _audit;

    private readonly IRepository<Specimen>? _specimens;

    public EncounterService(
        IRepository<Encounter> encounters,
        IRepository<Patient> patients,
        IRepository<OrderingProvider> providers,
        OrderingProviderService orderingProviders,
        IUnitOfWork unitOfWork,
        IClock clock,
        IPermissionEvaluator? permissions = null,
        ICurrentUser? currentUser = null,
        IAuditWriter? audit = null,
        IRepository<Specimen>? specimens = null)
    {
        _specimens = specimens;
        _encounters = encounters;
        _patients = patients;
        _providers = providers;
        _orderingProviders = orderingProviders;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _permissions = permissions;
        _currentUser = currentUser;
        _audit = audit;
    }

    public Task<IReadOnlyList<Encounter>> ListByPatientAsync(long patientId, CancellationToken ct = default) =>
        _encounters.ListAsync(e => e.PatientId == patientId, ct);

    public Task<Encounter?> GetAsync(long id, CancellationToken ct = default) =>
        _encounters.GetByIdAsync(id, ct);

    public async Task<OperationResult<Encounter>> CreateAsync(long patientId, CreateEncounterRequest request, CancellationToken ct = default)
    {
        var unauthorized = await RejectUnauthorizedAsync(EncounterAuthorizationRule.EvaluateCreate, ct);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        if (await _patients.GetByIdAsync(patientId, ct) is null)
        {
            return OperationResult<Encounter>.Fail("Patient not found.");
        }

        var merged = await RejectMergedPatientMessageAsync(patientId, ct);
        if (merged is not null)
        {
            return OperationResult<Encounter>.Fail(merged);
        }

        if (await _encounters.AnyAsync(e => e.VisitNumber == request.VisitNumber, ct))
        {
            return OperationResult<Encounter>.Fail($"Visit number '{request.VisitNumber}' already exists.");
        }

        var (providerId, providerName) = await ResolveAttendingProviderAsync(request.AttendingProviderId, ct);

        var encounter = new Encounter
        {
            PatientId = patientId,
            VisitNumber = request.VisitNumber.Trim(),
            EncounterType = request.EncounterType,
            Status = request.Status,
            AdmitUtc = request.AdmitUtc,
            DischargeUtc = request.DischargeUtc,
            AccountNumber = request.AccountNumber,
            AttendingProviderId = providerId,
            AttendingProvider = providerName,
            AdmissionLocation = request.AdmissionLocation,
            CurrentLocation = request.CurrentLocation,
            DischargeDisposition = request.DischargeDisposition,
            FinancialClass = request.FinancialClass,
            SourceSystem = request.SourceSystem ?? "Manual",
            ExternalVisitId = request.ExternalVisitId
        };

        var validation = EncounterValidator.Validate(encounter);
        if (validation.IsHardStopped)
        {
            return OperationResult<Encounter>.Fail(validation.HardStops.First().Message);
        }

        await _encounters.AddAsync(encounter, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _audit?.Record(
            AuditEventType.PatientAccess,
            nameof(Encounter),
            encounter.Id,
            newValue: Snapshot(encounter),
            reason: "Visit created.");
        if (_audit is not null)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<Encounter>.Ok(encounter);
    }

    public async Task<OperationResult<Encounter>> UpdateAsync(long patientId, long encounterId, UpdateEncounterRequest request, CancellationToken ct = default)
    {
        var unauthorized = await RejectUnauthorizedAsync(EncounterAuthorizationRule.EvaluateUpdate, ct);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var encounter = await _encounters.FirstOrDefaultAsync(e => e.Id == encounterId && e.PatientId == patientId, ct);
        if (encounter is null)
        {
            return OperationResult<Encounter>.Fail("Visit not found.");
        }

        var merged = await RejectMergedPatientMessageAsync(patientId, ct);
        if (merged is not null)
        {
            return OperationResult<Encounter>.Fail(merged);
        }

        var (providerId, providerName) = await ResolveAttendingProviderAsync(request.AttendingProviderId, ct);
        var old = Snapshot(encounter);

        encounter.EncounterType = request.EncounterType;
        encounter.Status = request.Status;
        encounter.AdmitUtc = request.AdmitUtc;
        encounter.DischargeUtc = request.DischargeUtc;
        await EndNeonatalAdmissionSpecimensAsync(encounter, ct);
        encounter.AccountNumber = request.AccountNumber;
        encounter.AttendingProviderId = providerId;
        encounter.AttendingProvider = providerName;
        encounter.AdmissionLocation = request.AdmissionLocation;
        encounter.CurrentLocation = request.CurrentLocation;
        encounter.DischargeDisposition = request.DischargeDisposition;
        encounter.FinancialClass = request.FinancialClass;

        var validation = EncounterValidator.Validate(encounter);
        if (validation.IsHardStopped)
        {
            return OperationResult<Encounter>.Fail(validation.HardStops.First().Message);
        }

        _encounters.Update(encounter);
        _audit?.Record(
            AuditEventType.PatientAccess,
            nameof(Encounter),
            encounter.Id,
            oldValue: old,
            newValue: Snapshot(encounter),
            reason: "Visit updated.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Encounter>.Ok(encounter);
    }

    public const string VisitOtherPatientCode = "ADT-VISIT-OTHER-PATIENT";

    /// <summary>
    /// Why an ADT message may not apply its visit: the visit number is already another patient's visit (not one merged
    /// into this patient). End-user testing pass 60: an A03 for MRN0022 naming UAT0001's visit moved the visit, and
    /// whatever was recorded on it, to MRN0022 and discharged it, acknowledged AA. Null when the visit is new or this
    /// patient's. <paramref name="patientId"/> is null for a patient the message would create.
    /// </summary>
    public async Task<string?> VisitOwnerConflictAsync(long? patientId, string? visitNumber, string messageMrn, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(visitNumber))
        {
            return null;
        }

        var visit = visitNumber.Trim();
        var encounter = await _encounters.FirstOrDefaultAsync(e => e.VisitNumber == visit, ct);
        if (encounter is null || (patientId is long id && encounter.PatientId == id))
        {
            return null;
        }

        var owner = await _patients.GetByIdAsync(encounter.PatientId, ct);
        if (owner is { Status: PatientStatus.Merged } && patientId is long survivor && owner.MergedIntoPatientId == survivor)
        {
            return null;
        }

        var ownerText = owner is null ? $"patient {encounter.PatientId}" : $"{owner.LastName}, {owner.FirstName} ({owner.MedicalRecordNumber})";
        return $"{VisitOtherPatientCode}: Visit {visit} belongs to {ownerText}, but this message is for {messageMrn}. "
            + "Nothing was applied. Correct the visit or patient in the EHR (a visit move is a separate message), then replay.";
    }

    /// <summary>
    /// Creates or updates a visit from ADT PV1 when a visit number is present.
    /// </summary>
    public async Task<string?> UpsertVisitFromHl7Async(
        long patientId,
        string visitNumber,
        string? accountNumber,
        DateTime? admitUtc,
        DateTime? dischargeUtc,
        string? currentLocation,
        string? attendingProviderId,
        string? attendingProviderName,
        string? triggerEvent,
        CancellationToken ct = default,
        EncounterType? patientType = null)
    {
        if (string.IsNullOrWhiteSpace(visitNumber))
        {
            return null;
        }

        var merged = await RejectMergedPatientMessageAsync(patientId, ct);
        if (merged is not null)
        {
            return merged;
        }

        OrderingProvider? attending = null;
        if (!string.IsNullOrWhiteSpace(attendingProviderId) && !string.IsNullOrWhiteSpace(attendingProviderName))
        {
            attending = await _orderingProviders.EnsureFromHl7Async(
                attendingProviderId,
                attendingProviderName,
                specialty: null,
                location: currentLocation,
                "HL7",
                ct);
        }

        var visit = visitNumber.Trim();
        var encounter = await _encounters.FirstOrDefaultAsync(e => e.VisitNumber == visit, ct);
        var isDischarge = string.Equals(triggerEvent, "A03", StringComparison.OrdinalIgnoreCase);
        var isCancelVisit = triggerEvent is "A11" or "A23";
        var isCancelDischarge = string.Equals(triggerEvent, "A13", StringComparison.OrdinalIgnoreCase);

        if (isCancelVisit)
        {
            if (encounter is null)
            {
                return $"Visit {visit} was not found; cancel was not applied.";
            }

            encounter.Status = EncounterStatus.Cancelled;
            encounter.PatientId = patientId;
            _encounters.Update(encounter);
            return $"Visit {visit} cancelled.";
        }

        if (encounter is null)
        {
            encounter = new Encounter
            {
                PatientId = patientId,
                VisitNumber = visit,
                AccountNumber = accountNumber,
                // PV1-2 patient class, translated; Unknown when the message does not say (it used to assume Inpatient).
                EncounterType = patientType ?? EncounterType.Unknown,
                Status = isDischarge ? EncounterStatus.Discharged : EncounterStatus.Active,
                AdmitUtc = admitUtc ?? _clock.UtcNow,
                DischargeUtc = isDischarge ? dischargeUtc ?? _clock.UtcNow : dischargeUtc,
                AttendingProviderId = attending?.Id,
                AttendingProvider = attending?.Name ?? attendingProviderName,
                CurrentLocation = currentLocation,
                SourceSystem = "HL7",
                ExternalVisitId = visit
            };
            await _encounters.AddAsync(encounter, ct);
            return $"Visit {visit} created.";
        }

        encounter.PatientId = patientId;
        if (accountNumber is not null) encounter.AccountNumber = accountNumber;
        if (admitUtc is not null) encounter.AdmitUtc = admitUtc;
        if (dischargeUtc is not null)
        {
            encounter.DischargeUtc = dischargeUtc;
            await EndNeonatalAdmissionSpecimensAsync(encounter, ct);
        }
        if (currentLocation is not null) encounter.CurrentLocation = currentLocation;
        if (patientType is not null) encounter.EncounterType = patientType.Value;
        if (attending is not null)
        {
            encounter.AttendingProviderId = attending.Id;
            encounter.AttendingProvider = attending.Name;
        }
        else if (!string.IsNullOrWhiteSpace(attendingProviderName))
        {
            encounter.AttendingProvider = attendingProviderName;
        }

        if (isDischarge)
        {
            encounter.Status = EncounterStatus.Discharged;
            encounter.DischargeUtc ??= _clock.UtcNow;
        }
        else if (isCancelDischarge && encounter.Status == EncounterStatus.Discharged)
        {
            encounter.Status = EncounterStatus.Active;
            encounter.DischargeUtc = null;
        }
        else if (encounter.Status == EncounterStatus.Discharged && triggerEvent is "A01" or "A04" or "A08")
        {
            encounter.Status = EncounterStatus.Active;
        }

        _encounters.Update(encounter);
        return $"Visit {visit} updated.";
    }

    /// <summary>
    /// Ensures an active HL7 interface encounter exists for order placement when ADT/PV1 is absent.
    /// </summary>
    public async Task<Encounter> EnsureInterfaceEncounterAsync(long patientId, CancellationToken ct = default)
    {
        var merged = await RejectMergedPatientMessageAsync(patientId, ct);
        if (merged is not null)
        {
            throw new InvalidOperationException(merged);
        }

        var existing = await _encounters.FirstOrDefaultAsync(
            e => e.PatientId == patientId && e.SourceSystem == "HL7" && e.Status == EncounterStatus.Active, ct);
        if (existing is not null)
        {
            return existing;
        }

        var visitNumber = $"HL7-{patientId}";
        if (await _encounters.FirstOrDefaultAsync(e => e.VisitNumber == visitNumber, ct) is { } byNumber)
        {
            return byNumber;
        }

        var encounter = new Encounter
        {
            PatientId = patientId,
            VisitNumber = visitNumber,
            EncounterType = EncounterType.Unknown,
            Status = EncounterStatus.Active,
            AdmitUtc = _clock.UtcNow,
            SourceSystem = "HL7",
            CurrentLocation = "Interface"
        };

        await _encounters.AddAsync(encounter, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return encounter;
    }

    public async Task<Encounter> EnsureEncounterForHl7OrderAsync(long patientId, string? visitNumber, CancellationToken ct = default)
    {
        var merged = await RejectMergedPatientMessageAsync(patientId, ct);
        if (merged is not null)
        {
            throw new InvalidOperationException(merged);
        }

        if (!string.IsNullOrWhiteSpace(visitNumber))
        {
            var byVisit = await _encounters.FirstOrDefaultAsync(
                e => e.PatientId == patientId && e.VisitNumber == visitNumber, ct);
            if (byVisit is not null)
            {
                return byVisit;
            }
        }

        return await EnsureInterfaceEncounterAsync(patientId, ct);
    }

    private async Task<(long? Id, string? Name)> ResolveAttendingProviderAsync(long? providerId, CancellationToken ct)
    {
        if (providerId is not > 0)
        {
            return (null, null);
        }

        var provider = await _providers.GetByIdAsync(providerId.Value, ct);
        if (provider is null)
        {
            return (null, null);
        }

        if (!provider.IsActive)
        {
            return (null, null);
        }

        return (provider.Id, provider.Name);
    }

    private static object Snapshot(Encounter encounter) => new
    {
        encounter.VisitNumber,
        encounter.EncounterType,
        encounter.Status,
        encounter.CurrentLocation,
        encounter.AttendingProvider,
        encounter.AdmitUtc,
        encounter.DischargeUtc
    };

    private async Task<string?> RejectMergedPatientMessageAsync(long patientId, CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return null;
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        return clinical.Severity == RuleSeverity.HardStop ? clinical.Message : null;
    }

    private async Task<OperationResult<Encounter>?> RejectUnauthorizedAsync(
        Func<bool, RuleResult> evaluate, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var userName = _currentUser?.UserName ?? string.Empty;
        var allowed = await _permissions.HasPermissionAsync(userName, PermissionCodes.PatientWrite, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<Encounter>.Fail(auth.Message)
            : null;
    }

    /// <summary>
    /// OCD-074: an infant's admission-long specimens on a discharged visit end at discharge (never before their usual
    /// expiration). Audited; saved with the visit.
    /// </summary>
    private async Task EndNeonatalAdmissionSpecimensAsync(Encounter encounter, CancellationToken ct)
    {
        if (_specimens is null || encounter.DischargeUtc is not DateTime discharged || encounter.Id == 0)
        {
            return;
        }

        var listed = await _specimens.ListAsync(s => s.EncounterId == encounter.Id && s.NeonatalStandardExpiresUtc != null, ct);
        foreach (var id in listed.Select(s => s.Id))
        {
            // The tracked row (the list may be untracked copies), changed in place and saved with the visit.
            if (await _specimens.GetByIdAsync(id, ct) is not { } specimen
                || specimen.ExpiresUtc is not DateTime current
                || NeonatalTestingRule.OnDischarge(current, specimen.NeonatalStandardExpiresUtc!.Value, discharged) is not { } next)
            {
                continue;
            }

            specimen.ExpiresUtc = next;
            _audit?.Record(AuditEventType.Specimen, nameof(Specimen), specimen.Id,
                oldValue: new { ExpiresUtc = current }, newValue: new { ExpiresUtc = next },
                reason: $"Visit {encounter.VisitNumber} discharged: the infant's admission specimen ends (OCD-074).");
        }
    }
}
