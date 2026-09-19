using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Specimens;

/// <summary>
/// Specimen accessioning, metadata edit, and rejection. Expiration is computed from the
/// specimen-type catalog offset, then shortened to the 72-hour alloimmunization window
/// when recent transfusion or pregnancy risk applies.
/// </summary>
public sealed class SpecimenService
{
    /// <summary>Default specimen validity window when no facility policy is registered.</summary>
    public const int DefaultValidityHours = SpecimenValidityPolicy.DefaultStandardHours;

    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<SpecimenTypeDefinition> _specimenTypes;
    private readonly IRepository<TransfusionEvent>? _transfusions;
    private readonly FacilityPolicyService? _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditWriter? _audit;
    private readonly ICurrentUser? _currentUser;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<AntibodyIdentificationWorkup>? _antibodyWorkups;
    private readonly IRepository<AntibodyIdentificationFinding>? _antibodyFindings;
    private readonly IRepository<ExceptionDefinition>? _exceptionDefinitions;
    private readonly IRepository<Override>? _overrides;

    public SpecimenService(
        IRepository<Specimen> specimens,
        IRepository<Patient> patients,
        IRepository<SpecimenTypeDefinition> specimenTypes,
        IUnitOfWork unitOfWork,
        IClock clock,
        IRepository<TransfusionEvent>? transfusions = null,
        FacilityPolicyService? policy = null,
        IAuditWriter? audit = null,
        ICurrentUser? currentUser = null,
        IPermissionEvaluator? permissions = null,
        IRepository<AntibodyIdentificationWorkup>? antibodyWorkups = null,
        IRepository<AntibodyIdentificationFinding>? antibodyFindings = null,
        IRepository<ExceptionDefinition>? exceptionDefinitions = null,
        IRepository<Override>? overrides = null)
    {
        _specimens = specimens;
        _patients = patients;
        _specimenTypes = specimenTypes;
        _transfusions = transfusions;
        _policy = policy;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
        _currentUser = currentUser;
        _permissions = permissions;
        _antibodyWorkups = antibodyWorkups;
        _antibodyFindings = antibodyFindings;
        _exceptionDefinitions = exceptionDefinitions;
        _overrides = overrides;
    }

    public async Task<SpecimenDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var specimen = await _specimens.GetByIdAsync(id, ct);
        return specimen is null ? null : await MapAsync(specimen, ct);
    }

    public async Task<IReadOnlyList<SpecimenDto>> GetByPatientAsync(long patientId, CancellationToken ct = default)
    {
        var specimens = await _specimens.ListAsync(s => s.PatientId == patientId, ct);
        var descriptions = await LoadActiveDescriptionMapAsync(ct);
        return specimens
            .OrderByDescending(s => s.CollectedUtc)
            .Select(s => SpecimenDto.From(s, ResolveDescription(s.SpecimenType, descriptions)))
            .ToList();
    }

    public async Task<OperationResult<Specimen>> AccessionAsync(AccessionSpecimenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.SpecimenAccession, SpecimenAuthorizationRule.EvaluateAccession, ct);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(request.AccessionNumber))
        {
            return OperationResult<Specimen>.Fail("Accession number is required.");
        }

        var typeCode = request.SpecimenType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(typeCode))
        {
            return OperationResult<Specimen>.Fail("Specimen type is required.");
        }

        var typeDef = await _specimenTypes.FirstOrDefaultAsync(
            t => t.IsActive && !t.IsDraft && t.Code == typeCode, ct);
        if (typeDef is null)
        {
            return OperationResult<Specimen>.Fail($"Specimen type '{typeCode}' is not in the active catalog.");
        }

        if (request.CollectedUtc > _clock.UtcNow)
        {
            return OperationResult<Specimen>.Fail("Collection date/time cannot be in the future.");
        }

        if (await _patients.GetByIdAsync(request.PatientId, ct) is not { } patient)
        {
            return OperationResult<Specimen>.Fail("Patient not found.");
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        if (clinical.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<Specimen>.Fail(clinical.Message);
        }

        if (await _specimens.AnyAsync(s => s.AccessionNumber == request.AccessionNumber, ct))
        {
            return OperationResult<Specimen>.Fail($"Accession number '{request.AccessionNumber}' already exists.");
        }

        var id1Type = request.Identifier1Type ?? IdentityTokenType.MedicalRecordNumber;
        var id1Value = string.IsNullOrWhiteSpace(request.Identifier1Value) ? patient.MedicalRecordNumber : request.Identifier1Value;
        var id2Type = request.Identifier2Type ?? IdentityTokenType.DateOfBirth;
        var id2Value = string.IsNullOrWhiteSpace(request.Identifier2Value)
            ? patient.DateOfBirth.ToString("yyyy-MM-dd")
            : request.Identifier2Value;

        var identity = PatientIdentityMatchRule.Evaluate(
            patient.MedicalRecordNumber,
            patient.DateOfBirth,
            patient.LastName,
            patient.FirstName,
            new PatientIdentityMatchRule.IdentityToken(id1Type, id1Value),
            new PatientIdentityMatchRule.IdentityToken(id2Type, id2Value));
        if (identity.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<Specimen>.Fail(identity.Message);
        }

        var expiresUtc = await ComputeExpiresUtcAsync(
            request.CollectedUtc, typeDef, patient, request.ValidityHours, ct);
        var specimen = new Specimen
        {
            AccessionNumber = request.AccessionNumber,
            PatientId = request.PatientId,
            SpecimenType = typeCode,
            Barcode = request.Barcode,
            CollectedUtc = request.CollectedUtc,
            ReceivedUtc = _clock.UtcNow,
            ExpiresUtc = expiresUtc,
            DrawLocation = request.DrawLocation,
            Collector = request.Collector,
            Identifier1Type = id1Type,
            Identifier1Value = id1Value,
            Identifier2Type = id2Type,
            Identifier2Value = id2Value,
            Status = SpecimenStatus.Accepted,
            Comment = NormalizeComment(request.Comment)
        };

        await _specimens.AddAsync(specimen, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit?.Record(
            AuditEventType.Specimen,
            nameof(Specimen),
            specimen.Id,
            newValue: new { specimen.AccessionNumber, specimen.SpecimenType, specimen.CollectedUtc, specimen.ExpiresUtc, specimen.Status },
            reason: "Specimen accessioned.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Specimen>.Ok(specimen);
    }

    /// <summary>
    /// Updates collection metadata on an accepted specimen. Accession number, type,
    /// patient, identity tokens, and status are immutable here.
    /// </summary>
    public async Task<OperationResult<Specimen>> UpdateAsync(long specimenId, UpdateSpecimenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.SpecimenEdit, SpecimenAuthorizationRule.EvaluateEdit, ct);
        if (denied is not null)
        {
            return denied;
        }

        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null)
        {
            return OperationResult<Specimen>.Fail("Specimen not found.");
        }

        if (specimen.Status is not SpecimenStatus.Accepted)
        {
            return OperationResult<Specimen>.Fail($"A specimen with status {specimen.Status} cannot be edited.");
        }

        if (request.CollectedUtc > _clock.UtcNow)
        {
            return OperationResult<Specimen>.Fail("Collection date/time cannot be in the future.");
        }

        var patient = await _patients.GetByIdAsync(specimen.PatientId, ct);
        var typeDef = await _specimenTypes.FirstOrDefaultAsync(
            t => t.IsActive && !t.IsDraft && t.Code == specimen.SpecimenType, ct);
        var policyExpires = await ComputeExpiresUtcAsync(
            request.CollectedUtc, typeDef, patient, request.ValidityHours, ct);
        var nextExpires = request.ExpiresUtc ?? policyExpires;
        if (nextExpires < request.CollectedUtc)
        {
            return OperationResult<Specimen>.Fail("Expiration cannot be before collection.");
        }

        var expirationRule = request.ExpiresUtc is null
            ? RuleResult.Pass(SpecimenExpirationOverrideRule.Code)
            : SpecimenExpirationOverrideRule.Evaluate(policyExpires, request.ExpiresUtc.Value, overrideAuthorized: false);
        var overrideNeeded = expirationRule.Severity == RuleSeverity.Warning;
        if (overrideNeeded)
        {
            var blocked = await EvaluateExpirationOverrideAccessAsync(request, expirationRule, ct);
            if (blocked is not null)
            {
                return blocked;
            }
        }

        var previous = new
        {
            specimen.CollectedUtc,
            specimen.Barcode,
            specimen.DrawLocation,
            specimen.Collector,
            specimen.ExpiresUtc,
            specimen.Comment
        };

        var validityChanged = specimen.CollectedUtc != request.CollectedUtc
            || specimen.ExpiresUtc != nextExpires;

        specimen.CollectedUtc = request.CollectedUtc;
        specimen.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        specimen.DrawLocation = string.IsNullOrWhiteSpace(request.DrawLocation) ? null : request.DrawLocation.Trim();
        specimen.Collector = string.IsNullOrWhiteSpace(request.Collector) ? null : request.Collector.Trim();
        specimen.ExpiresUtc = nextExpires;
        specimen.Comment = NormalizeComment(request.Comment);

        if (overrideNeeded && _overrides is not null)
        {
            await _overrides.AddAsync(new Override
            {
                Action = OverrideAction.WarningOverride,
                ContextType = nameof(Specimen),
                ContextId = specimen.Id,
                RuleCode = SpecimenExpirationOverrideRule.Code,
                Reason = request.OverrideReason!.Trim(),
                AuthorizedBy = request.AuthorizedBy!.Trim(),
                OverriddenUtc = _clock.UtcNow
            }, ct);
        }

        _specimens.Update(specimen);
        _audit?.Record(
            AuditEventType.Specimen,
            nameof(Specimen),
            specimen.Id,
            oldValue: previous,
            newValue: new
            {
                specimen.CollectedUtc,
                specimen.Barcode,
                specimen.DrawLocation,
                specimen.Collector,
                specimen.ExpiresUtc,
                specimen.Comment,
                RuleCode = overrideNeeded ? SpecimenExpirationOverrideRule.Code : null,
                OverrideReason = overrideNeeded ? request.OverrideReason : null
            },
            reason: overrideNeeded
                ? $"Specimen metadata updated with {SpecimenExpirationOverrideRule.Code}."
                : "Specimen metadata updated.");
        await _unitOfWork.SaveChangesAsync(ct);

        var warnings = new List<RuleResult>();
        if (validityChanged)
        {
            warnings.AddRange(await WarnAndWithdrawOpenWorkupAsync(
                specimen.Id,
                AntibodyIdentificationHistoryPostRule.EvaluateSpecimenEditedOpenWorkup,
                "Linked specimen collection or expiration changed while an antibody-identification workup was open. Interpretation and supervisor review must be repeated against the current specimen validity. This does not identify antibodies.",
                ct));
        }

        return OperationResult<Specimen>.Ok(specimen, warnings);
    }

    public async Task<OperationResult<Specimen>> RejectAsync(long specimenId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<Specimen>.Fail("A reason is required to reject a specimen.");
        }

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.SpecimenReject, SpecimenAuthorizationRule.EvaluateReject, ct);
        if (denied is not null)
        {
            return denied;
        }

        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null)
        {
            return OperationResult<Specimen>.Fail("Specimen not found.");
        }

        if (specimen.Status is SpecimenStatus.Rejected or SpecimenStatus.Cancelled or SpecimenStatus.Expired)
        {
            return OperationResult<Specimen>.Fail($"A specimen with status {specimen.Status} cannot be rejected.");
        }

        var previousStatus = specimen.Status;
        specimen.Status = SpecimenStatus.Rejected;
        specimen.RejectionReason = reason;
        _specimens.Update(specimen);
        _audit?.Record(
            AuditEventType.Specimen,
            nameof(Specimen),
            specimen.Id,
            oldValue: new { Status = previousStatus },
            newValue: new { specimen.Status, specimen.RejectionReason },
            reason: reason.Trim());
        await _unitOfWork.SaveChangesAsync(ct);

        var warnings = await WarnAndWithdrawOpenWorkupAsync(
            specimen.Id,
            AntibodyIdentificationHistoryPostRule.EvaluateSpecimenRejectedOpenWorkup,
            "Linked specimen was rejected while an antibody-identification workup was open. Interpretation and supervisor review must be repeated, or the workup voided, before completing. This does not identify antibodies.",
            ct);
        return OperationResult<Specimen>.Ok(specimen, warnings);
    }

    /// <summary>
    /// Recomputes expiry for accepted specimens when alloimmunization risk changes
    /// (recent transfusion or documented pregnancy).
    /// </summary>
    public async Task RecomputeValidityForPatientAsync(long patientId, CancellationToken ct = default)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return;
        }

        var types = await _specimenTypes.ListAsync(t => t.IsActive && !t.IsDraft, ct);
        var typesByCode = types.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        var specimens = await _specimens.ListAsync(
            s => s.PatientId == patientId && s.Status == SpecimenStatus.Accepted, ct);
        var changedIds = new List<long>();
        HashSet<long> overriddenIds = [];
        if (_overrides is not null)
        {
            var overrides = await _overrides.ListAsync(
                o => o.ContextType == nameof(Specimen)
                    && o.RuleCode == SpecimenExpirationOverrideRule.Code,
                ct);
            overriddenIds = overrides.Select(o => o.ContextId).ToHashSet();
        }

        foreach (var specimen in specimens)
        {
            if (overriddenIds.Contains(specimen.Id))
            {
                continue;
            }

            typesByCode.TryGetValue(specimen.SpecimenType, out var typeDef);
            var next = await ComputeExpiresUtcAsync(specimen.CollectedUtc, typeDef, patient, validityHoursOverride: null, ct);
            if (specimen.ExpiresUtc == next)
            {
                continue;
            }

            var previous = specimen.ExpiresUtc;
            specimen.ExpiresUtc = next;
            _specimens.Update(specimen);
            changedIds.Add(specimen.Id);
            _audit?.Record(
                AuditEventType.Specimen,
                nameof(Specimen),
                specimen.Id,
                oldValue: new { ExpiresUtc = previous },
                newValue: new { ExpiresUtc = next },
                reason: "Specimen validity recomputed after alloimmunization-risk change.");
        }

        await _unitOfWork.SaveChangesAsync(ct);
        foreach (var specimenId in changedIds)
        {
            await WarnAndWithdrawOpenWorkupAsync(
                specimenId,
                AntibodyIdentificationHistoryPostRule.EvaluateSpecimenEditedOpenWorkup,
                "Linked specimen expiration was recomputed while an antibody-identification workup was open. Interpretation and supervisor review must be repeated against the current specimen validity. This does not identify antibodies.",
                ct);
        }
    }

    private async Task<List<RuleResult>> WarnAndWithdrawOpenWorkupAsync(
        long specimenId,
        Func<bool, RuleResult> evaluate,
        string withdrawReason,
        CancellationToken ct)
    {
        var warnings = new List<RuleResult>();
        if (_antibodyWorkups is null)
        {
            return warnings;
        }

        var hasOpen = await _antibodyWorkups.AnyAsync(
            w => w.SpecimenId == specimenId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);
        var open = evaluate(hasOpen);
        if (open.Severity != RuleSeverity.Warning)
        {
            return warnings;
        }

        await AntibodyIdentificationJudgmentWithdrawal.WithdrawAfterLinkedSpecimenUnusableAsync(
            _antibodyWorkups,
            _antibodyFindings,
            _audit,
            _unitOfWork,
            specimenId,
            withdrawReason,
            ct);
        warnings.Add(open);
        return warnings;
    }

    private async Task<DateTime> ComputeExpiresUtcAsync(
        DateTime collectedUtc,
        SpecimenTypeDefinition? typeDef,
        Patient? patient,
        int? validityHoursOverride,
        CancellationToken ct)
    {
        DateTime catalogExpires;
        if (validityHoursOverride is > 0)
        {
            catalogExpires = collectedUtc.AddHours(validityHoursOverride.Value);
        }
        else if (typeDef is not null && SpecimenExpirationCode.TryParse(typeDef.ExpirationCode, out var code))
        {
            catalogExpires = SpecimenExpirationCalculator.ComputeExpiresUtc(
                collectedUtc, code, typeDef.ExpirationMode);
        }
        else
        {
            catalogExpires = collectedUtc.AddHours(DefaultValidityHours);
        }

        if (patient is null)
        {
            return catalogExpires;
        }

        var (hasRisk, alloHours) = await ResolveAlloRiskAsync(patient, ct);
        if (!hasRisk)
        {
            return catalogExpires;
        }

        var alloExpires = collectedUtc.AddHours(alloHours);
        return catalogExpires <= alloExpires ? catalogExpires : alloExpires;
    }

    private async Task<(bool HasRisk, int AlloHours)> ResolveAlloRiskAsync(Patient patient, CancellationToken ct)
    {
        var alloHours = _policy is null
            ? SpecimenValidityPolicy.DefaultAlloimmunizationRiskHours
            : await _policy.GetSpecimenAlloHoursAsync(ct);
        var lookbackDays = _policy is null
            ? SpecimenValidityPolicy.DefaultLookbackDays
            : await _policy.GetSpecimenLookbackDaysAsync(ct);

        DateTime? lastTransfusion = null;
        if (_transfusions is not null)
        {
            var events = await _transfusions.ListAsync(t => t.PatientId == patient.Id, ct);
            if (events.Count > 0)
            {
                lastTransfusion = events.Max(t => t.StartUtc ?? t.CreatedUtc);
            }
        }

        var risk = SpecimenValidityPolicy.HasAlloimmunizationRisk(
            _clock.UtcNow, lastTransfusion, patient.RecentPregnancyUtc, lookbackDays);
        return (risk, alloHours);
    }

    private async Task<SpecimenDto> MapAsync(Specimen specimen, CancellationToken ct)
    {
        var descriptions = await LoadActiveDescriptionMapAsync(ct);
        return SpecimenDto.From(specimen, ResolveDescription(specimen.SpecimenType, descriptions));
    }

    private async Task<Dictionary<string, string>> LoadActiveDescriptionMapAsync(CancellationToken ct)
    {
        var types = await _specimenTypes.ListAsync(t => t.IsActive && !t.IsDraft, ct);
        return types.ToDictionary(t => t.Code, t => t.Description, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveDescription(string typeCode, IReadOnlyDictionary<string, string> descriptions) =>
        descriptions.TryGetValue(typeCode, out var description) ? description : null;

    private static string? NormalizeComment(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<OperationResult<Specimen>?> EvaluateExpirationOverrideAccessAsync(
        UpdateSpecimenRequest request,
        RuleResult warning,
        CancellationToken ct)
    {
        var overrideAttempted = !string.IsNullOrWhiteSpace(request.OverrideReason)
            && !string.IsNullOrWhiteSpace(request.AuthorizedBy);
        if (!overrideAttempted)
        {
            return OperationResult<Specimen>.Fail(warning.Message);
        }

        if (_exceptionDefinitions is null || _permissions is null || _currentUser is null)
        {
            return OperationResult<Specimen>.Fail(warning.Message);
        }

        var definition = await _exceptionDefinitions.FirstOrDefaultAsync(
            e => e.RuleCode == SpecimenExpirationOverrideRule.Code && e.IsActive, ct);
        var userLevel = await _permissions.GetMaxSecurityLevelAsync(_currentUser.UserName, ct);
        var access = ExceptionOverridePolicy.EvaluateAccess(
            userLevel, definition, SpecimenExpirationOverrideRule.Code);
        if (access.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<Specimen>.Fail(access.Message);
        }

        return null;
    }

    private async Task<OperationResult<Specimen>?> RejectUnauthorizedAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var userName = _currentUser?.UserName ?? string.Empty;
        var allowed = await _permissions.HasPermissionAsync(userName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<Specimen>.Fail(auth.Message)
            : null;
    }
}
