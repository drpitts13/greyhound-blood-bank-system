using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Patients;

/// <summary>
/// Patient demographic create and update. MRN is immutable after create; ADT inbound may still
/// overwrite name, date of birth, and sex on a later message without this service.
/// Name changes retain the previous display name as a historical alias with a required reason.
/// </summary>
public sealed class PatientService
{
    private const int NameMaxLength = 100;

    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientIdentifier>? _identifiers;
    private readonly PatientHistoricalAliasWriter? _aliasWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly SpecimenService _specimens;
    private readonly IAuditWriter? _audit;
    private readonly ICurrentUser? _currentUser;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<PatientBloodTypeHistory>? _bloodTypes;
    private readonly IRepository<AntibodyHistory>? _antibodies;
    private readonly IRepository<SpecialTransfusionRequirement>? _requirements;
    private readonly IRepository<SpecialRequirementDefinition>? _requirementDefinitions;
    private readonly IRepository<Encounter>? _encounters;

    public PatientService(
        IRepository<Patient> patients,
        IUnitOfWork unitOfWork,
        IClock clock,
        SpecimenService specimens,
        IAuditWriter? audit = null,
        ICurrentUser? currentUser = null,
        IPermissionEvaluator? permissions = null,
        IRepository<PatientIdentifier>? identifiers = null,
        PatientHistoricalAliasWriter? aliasWriter = null,
        IRepository<PatientBloodTypeHistory>? bloodTypes = null,
        IRepository<AntibodyHistory>? antibodies = null,
        IRepository<SpecialTransfusionRequirement>? requirements = null,
        IRepository<SpecialRequirementDefinition>? requirementDefinitions = null,
        IRepository<Encounter>? encounters = null)
    {
        _encounters = encounters;
        _requirementDefinitions = requirementDefinitions;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _requirements = requirements;
        _patients = patients;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _specimens = specimens;
        _audit = audit;
        _currentUser = currentUser;
        _permissions = permissions;
        _identifiers = identifiers;
        _aliasWriter = aliasWriter ?? (identifiers is null ? null : new PatientHistoricalAliasWriter(identifiers));
    }

    public async Task<IReadOnlyList<PatientDto>> ListDtosAsync(CancellationToken ct = default)
    {
        var patients = await _patients.ListAsync(ct);
        var aliases = _identifiers is null
            ? Array.Empty<PatientIdentifier>()
            : await _identifiers.ListAsync(ct);
        var byPatient = aliases
            .Where(IsHistoricalAlias)
            .GroupBy(a => a.PatientId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PatientAliasDto>)g
                .OrderBy(a => a.IdentifierType)
                .ThenBy(a => a.Value, StringComparer.OrdinalIgnoreCase)
                .Select(PatientAliasDto.From)
                .ToList());

        var now = _clock.UtcNow;
        var currentTypes = _bloodTypes is null
            ? Array.Empty<PatientBloodTypeHistory>().ToLookup(t => t.PatientId)
            : (await _bloodTypes.ListAsync(t => t.IsCurrent && t.InvalidatedUtc == null, ct)).ToLookup(t => t.PatientId);
        var antibodies = _antibodies is null
            ? Array.Empty<AntibodyHistory>().ToLookup(a => a.PatientId)
            : (await _antibodies.ListAsync(a => a.IsActive, ct)).ToLookup(a => a.PatientId);
        var requirements = _requirements is null
            ? Array.Empty<SpecialTransfusionRequirement>().ToLookup(r => r.PatientId)
            : (await _requirements.ListAsync(r => r.IsActive, ct)).ToLookup(r => r.PatientId);
        var informational = _requirementDefinitions is null
            ? null
            : PatientBloodBankFlags.InformationalIds(await _requirementDefinitions.ListAsync(ct));
        var openVisits = _encounters is null
            ? Array.Empty<Encounter>().ToLookup(e => e.PatientId)
            : (await _encounters.ListAsync(e => e.Status == EncounterStatus.Active && e.DischargeUtc == null, ct)).ToLookup(e => e.PatientId);

        return patients
            .OrderBy(p => p.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.MedicalRecordNumber, StringComparer.OrdinalIgnoreCase)
            .Select(p => PatientDto.From(p, byPatient.GetValueOrDefault(p.Id)) with
            {
                CurrentBloodType = PatientBloodBankFlags.CurrentBloodType(currentTypes[p.Id]),
                ActiveAntibodies = PatientBloodBankFlags.ActiveAntibodies(antibodies[p.Id]),
                ActiveSpecialRequirements = PatientBloodBankFlags.RequirementsInEffect(requirements[p.Id], now, informational),
                InformationalNotes = PatientBloodBankFlags.NotesInEffect(requirements[p.Id], now, informational)
            })
            .Select(d =>
            {
                var (visit, otherOpen) = CurrentVisit.For(openVisits[d.Id]);
                return visit is null
                    ? d
                    : d with { CurrentVisitNumber = visit.VisitNumber, CurrentVisitWhere = CurrentVisit.Where(visit), OtherOpenVisits = otherOpen };
            })
            .ToList();
    }

    public async Task<PatientDto?> GetDtoAsync(long id, CancellationToken ct = default)
    {
        var patient = await _patients.GetByIdAsync(id, ct);
        if (patient is null)
        {
            return null;
        }

        return PatientDto.From(patient, await ListAliasesAsync(id, ct));
    }

    public async Task<IReadOnlyList<PatientAliasDto>> ListAliasesAsync(long patientId, CancellationToken ct = default)
    {
        if (_identifiers is null)
        {
            return Array.Empty<PatientAliasDto>();
        }

        // Spelled out rather than calling IsHistoricalAlias: the database cannot run a C# method,
        // and the untranslatable query stopped every patient chart from opening.
        var rows = await _identifiers.ListAsync(i => i.PatientId == patientId
            && (i.IdentifierType == IdentityTokenType.PriorFullName
                || i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber), ct);
        return rows
            .OrderBy(a => a.IdentifierType)
            .ThenBy(a => a.Value, StringComparer.OrdinalIgnoreCase)
            .Select(PatientAliasDto.From)
            .ToList();
    }

    /// <summary>
    /// Records an append-only chart-open event. List/search is not treated as access.
    /// </summary>
    public async Task RecordAccessAsync(long patientId, CancellationToken ct = default)
    {
        if (_audit is null)
        {
            return;
        }

        _audit.Record(
            AuditEventType.PatientAccess,
            nameof(Patient),
            patientId,
            newValue: new { PatientId = patientId },
            reason: "Patient record opened.");
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Active patients who may be the person being registered (<see cref="PossibleDuplicatePatientRule"/>), so the
    /// registrar can open the existing chart instead of making a second one. Needs the same permission as registering.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<PatientDto>>> ListPossibleDuplicatesAsync(
        string? lastName, string? firstName, DateOnly dateOfBirth, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(PatientAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return OperationResult<IReadOnlyList<PatientDto>>.Fail(denied.Error ?? "Not permitted.");
        }

        var matches = await FindPossibleDuplicatePatientsAsync(lastName, firstName, dateOfBirth, ct);
        return OperationResult<IReadOnlyList<PatientDto>>.Ok(matches.Select(p => PatientDto.From(p)).ToList());
    }

    /// <summary>
    /// Pairs of active charts that may be one person, by the same rule as registration, including patients registered
    /// by ADT. Each comes with its blood bank flags (type, antibodies, requirements) so staff can judge which chart
    /// holds the history before merging. Read-only; merging stays on the chart (patient.merge).
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<PossibleDuplicatePairDto>>> ListPossibleDuplicatePairsAsync(CancellationToken ct = default)
    {
        if (_permissions is not null)
        {
            var auth = PatientAuthorizationRule.EvaluateMerge(
                await _permissions.HasPermissionAsync(_currentUser?.UserName ?? string.Empty, PermissionCodes.PatientMerge, ct));
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<IReadOnlyList<PossibleDuplicatePairDto>>.Fail(auth.Message);
            }
        }

        var active = (await ListDtosAsync(ct)).Where(p => p.Status != PatientStatus.Merged).ToList();
        var pairs = new List<PossibleDuplicatePairDto>();
        foreach (var sameBirthday in active.GroupBy(p => p.DateOfBirth))
        {
            var group = sameBirthday.OrderBy(p => p.CreatedUtc).ThenBy(p => p.Id).ToList();
            for (var i = 0; i < group.Count; i++)
            {
                for (var j = i + 1; j < group.Count; j++)
                {
                    if (PossibleDuplicatePatientRule.IsPossibleDuplicate(
                            group[i].LastName, group[i].FirstName, group[i].DateOfBirth,
                            group[j].LastName, group[j].FirstName, group[j].DateOfBirth))
                    {
                        pairs.Add(new PossibleDuplicatePairDto(group[i], group[j]));
                    }
                }
            }
        }

        return OperationResult<IReadOnlyList<PossibleDuplicatePairDto>>.Ok(
            pairs.OrderBy(p => p.First.LastName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.First.MedicalRecordNumber).ToList());
    }

    private async Task<List<Patient>> FindPossibleDuplicatePatientsAsync(
        string? lastName, string? firstName, DateOnly dateOfBirth, CancellationToken ct)
    {
        if (dateOfBirth == default)
        {
            return [];
        }

        var sameBirthday = await _patients.ListAsync(p => p.DateOfBirth == dateOfBirth && p.Status != PatientStatus.Merged, ct);
        return sameBirthday
            .Where(p => PossibleDuplicatePatientRule.IsPossibleDuplicate(lastName, firstName, dateOfBirth, p.LastName, p.FirstName, p.DateOfBirth))
            .OrderBy(p => p.MedicalRecordNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string DescribeForMatch(Patient p) =>
        $"MRN {p.MedicalRecordNumber} ({p.LastName}, {p.FirstName}, born {p.DateOfBirth:yyyy-MM-dd})";

    public async Task<OperationResult<Patient>> CreateAsync(CreatePatientRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PatientAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var mrn = request.MedicalRecordNumber?.Trim() ?? string.Empty;
        if (mrn.Length == 0)
        {
            return OperationResult<Patient>.Fail("Medical record number is required.");
        }

        var lastName = request.LastName?.Trim() ?? string.Empty;
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var middleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();

        if (lastName.Length == 0)
        {
            return OperationResult<Patient>.Fail("Last name is required.");
        }

        if (firstName.Length == 0)
        {
            return OperationResult<Patient>.Fail("First name is required.");
        }

        if (lastName.Length > NameMaxLength || firstName.Length > NameMaxLength || (middleName?.Length ?? 0) > NameMaxLength)
        {
            return OperationResult<Patient>.Fail($"Name fields cannot exceed {NameMaxLength} characters.");
        }

        if (request.DateOfBirth == default)
        {
            return OperationResult<Patient>.Fail("Date of birth is required.");
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        if (request.DateOfBirth > today)
        {
            return OperationResult<Patient>.Fail("Date of birth cannot be in the future.");
        }

        var duplicate = await _patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == mrn, ct);
        if (duplicate is not null)
        {
            return OperationResult<Patient>.Fail("A patient with this medical record number already exists.");
        }

        // Another chart for the same person splits the ABO/Rh and antibody history; every possible match must be
        // confirmed as a different person before a new chart is made.
        var possible = await FindPossibleDuplicatePatientsAsync(lastName, firstName, request.DateOfBirth, ct);
        var confirmed = new HashSet<string>(
            (request.ConfirmedDifferentFromMrns ?? []).Select(m => m.Trim()), StringComparer.OrdinalIgnoreCase);
        var unconfirmed = possible.Where(p => !confirmed.Contains(p.MedicalRecordNumber)).ToList();
        if (unconfirmed.Count > 0)
        {
            return OperationResult<Patient>.Fail(PossibleDuplicatePatientRule.Message(unconfirmed.Select(DescribeForMatch)));
        }

        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = lastName,
            FirstName = firstName,
            MiddleName = middleName,
            DateOfBirth = request.DateOfBirth,
            Sex = request.Sex,
            Comment = NormalizeComment(request.Comment)
        };
        await _patients.AddAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _audit?.Record(
            AuditEventType.PatientAccess,
            nameof(Patient),
            patient.Id,
            newValue: new
            {
                patient.MedicalRecordNumber,
                patient.LastName,
                patient.FirstName,
                patient.MiddleName,
                patient.DateOfBirth,
                patient.Sex,
                ConfirmedDifferentFrom = possible.Count == 0 ? null : possible.Select(p => p.MedicalRecordNumber).ToList()
            },
            reason: possible.Count == 0
                ? "Patient created."
                : $"Patient created; confirmed a different person from possible duplicate(s) {string.Join(", ", possible.Select(p => p.MedicalRecordNumber))}.");
        if (_audit is not null)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<Patient>.Ok(patient);
    }

    public async Task<OperationResult<Patient>> UpdateAsync(long id, UpdatePatientRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PatientAuthorizationRule.EvaluateWrite, ct);
        if (denied is not null)
        {
            return denied;
        }

        var patient = await _patients.GetByIdAsync(id, ct);
        if (patient is null)
        {
            return OperationResult<Patient>.Fail("Patient not found.");
        }

        if (patient.Status == PatientStatus.Merged)
        {
            return OperationResult<Patient>.Fail("A merged patient cannot be edited.");
        }

        if (request.Status == PatientStatus.Merged)
        {
            return OperationResult<Patient>.Fail("Patient merge is not supported from demographics edit.");
        }

        var lastName = request.LastName?.Trim() ?? string.Empty;
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var middleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();

        if (lastName.Length == 0)
        {
            return OperationResult<Patient>.Fail("Last name is required.");
        }

        if (firstName.Length == 0)
        {
            return OperationResult<Patient>.Fail("First name is required.");
        }

        if (lastName.Length > NameMaxLength || firstName.Length > NameMaxLength || (middleName?.Length ?? 0) > NameMaxLength)
        {
            return OperationResult<Patient>.Fail($"Name fields cannot exceed {NameMaxLength} characters.");
        }

        if (request.DateOfBirth == default)
        {
            return OperationResult<Patient>.Fail("Date of birth is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Race) && !PatientDemographicCodes.IsRace(request.Race))
        {
            return OperationResult<Patient>.Fail($"'{request.Race.Trim()}' is not a race on the list.");
        }

        if (!string.IsNullOrWhiteSpace(request.Ethnicity) && !PatientDemographicCodes.IsEthnicity(request.Ethnicity))
        {
            return OperationResult<Patient>.Fail($"'{request.Ethnicity.Trim()}' is not an ethnicity on the list.");
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        if (request.DateOfBirth > today)
        {
            return OperationResult<Patient>.Fail("Date of birth cannot be in the future.");
        }

        var nameChanged = PatientHistoricalAliasRule.NameChanged(
            patient.LastName, patient.FirstName, patient.MiddleName,
            lastName, firstName, middleName);
        var reasonRule = PatientHistoricalAliasRule.EvaluateNameChangeReason(
            nameChanged, request.NameChangeReason);
        if (reasonRule.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<Patient>.Fail(reasonRule.Message);
        }

        var previous = new
        {
            patient.LastName,
            patient.FirstName,
            patient.MiddleName,
            patient.DateOfBirth,
            patient.Sex,
            patient.Status,
            patient.RecentPregnancyUtc,
            patient.Comment,
            patient.Race,
            patient.Ethnicity
        };

        var pregnancyChanged = patient.RecentPregnancyUtc != request.RecentPregnancyUtc;
        string? archivedName = null;
        if (nameChanged && _aliasWriter is not null)
        {
            archivedName = await _aliasWriter.ArchivePriorNameIfChangedAsync(
                patient.Id,
                patient.LastName, patient.FirstName, patient.MiddleName,
                lastName, firstName, middleName,
                ct);
        }

        patient.LastName = lastName;
        patient.FirstName = firstName;
        patient.MiddleName = middleName;
        patient.DateOfBirth = request.DateOfBirth;
        patient.Sex = request.Sex;
        patient.Status = request.Status;
        patient.RecentPregnancyUtc = request.RecentPregnancyUtc;
        patient.Comment = NormalizeComment(request.Comment);
        if (request.Race is not null)
        {
            patient.Race = string.IsNullOrWhiteSpace(request.Race) ? null : PatientDemographicCodes.NormalizeRace(request.Race);
        }

        if (request.Ethnicity is not null)
        {
            patient.Ethnicity = string.IsNullOrWhiteSpace(request.Ethnicity) ? null : PatientDemographicCodes.NormalizeEthnicity(request.Ethnicity);
        }

        _patients.Update(patient);
        _audit?.Record(
            AuditEventType.PatientAccess,
            nameof(Patient),
            patient.Id,
            oldValue: previous,
            newValue: new
            {
                patient.LastName,
                patient.FirstName,
                patient.MiddleName,
                patient.DateOfBirth,
                patient.Sex,
                patient.Status,
                patient.RecentPregnancyUtc,
                patient.Comment,
                patient.Race,
                patient.Ethnicity,
                ArchivedPriorName = archivedName,
                NameChangeReason = nameChanged ? request.NameChangeReason?.Trim() : null
            },
            reason: nameChanged
                ? $"Patient name changed: {request.NameChangeReason!.Trim()}"
                : "Patient demographics updated.");
        await _unitOfWork.SaveChangesAsync(ct);

        if (pregnancyChanged)
        {
            await _specimens.RecomputeValidityForPatientAsync(id, ct);
        }

        return OperationResult<Patient>.Ok(patient);
    }

    private static bool IsHistoricalAlias(PatientIdentifier identifier) =>
        identifier.IdentifierType is IdentityTokenType.PriorFullName
            or IdentityTokenType.PriorMedicalRecordNumber;

    private async Task<OperationResult<Patient>?> RejectUnauthorizedAsync(
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
            ? OperationResult<Patient>.Fail(auth.Message)
            : null;
    }

    private static string? NormalizeComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 2000 ? trimmed[..2000] : trimmed;
    }
}
