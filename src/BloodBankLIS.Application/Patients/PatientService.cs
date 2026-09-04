using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Patients;

/// <summary>
/// Patient demographic updates. MRN is immutable here; ADT inbound may still
/// overwrite name, date of birth, and sex on a later message.
/// </summary>
public sealed class PatientService
{
    private const int NameMaxLength = 100;

    private readonly IRepository<Patient> _patients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly SpecimenService _specimens;
    private readonly IAuditWriter? _audit;
    private readonly ICurrentUser? _currentUser;
    private readonly IPermissionEvaluator? _permissions;

    public PatientService(
        IRepository<Patient> patients,
        IUnitOfWork unitOfWork,
        IClock clock,
        SpecimenService specimens,
        IAuditWriter? audit = null,
        ICurrentUser? currentUser = null,
        IPermissionEvaluator? permissions = null)
    {
        _patients = patients;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _specimens = specimens;
        _audit = audit;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<OperationResult<Patient>> UpdateAsync(long id, UpdatePatientRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_permissions is not null)
        {
            var userName = _currentUser?.UserName ?? string.Empty;
            var allowed = await _permissions.HasPermissionAsync(userName, PermissionCodes.PatientWrite, ct);
            var auth = PatientAuthorizationRule.EvaluateWrite(allowed);
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<Patient>.Fail(auth.Message);
            }
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

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        if (request.DateOfBirth > today)
        {
            return OperationResult<Patient>.Fail("Date of birth cannot be in the future.");
        }

        var previous = new
        {
            patient.LastName,
            patient.FirstName,
            patient.MiddleName,
            patient.DateOfBirth,
            patient.Sex,
            patient.Status,
            patient.RecentPregnancyUtc
        };

        var pregnancyChanged = patient.RecentPregnancyUtc != request.RecentPregnancyUtc;

        patient.LastName = lastName;
        patient.FirstName = firstName;
        patient.MiddleName = middleName;
        patient.DateOfBirth = request.DateOfBirth;
        patient.Sex = request.Sex;
        patient.Status = request.Status;
        patient.RecentPregnancyUtc = request.RecentPregnancyUtc;

        _patients.Update(patient);
        _audit?.Record(
            AuditEventType.Update,
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
                patient.RecentPregnancyUtc
            },
            reason: "Patient demographics updated.");
        await _unitOfWork.SaveChangesAsync(ct);

        if (pregnancyChanged)
        {
            await _specimens.RecomputeValidityForPatientAsync(id, ct);
        }

        return OperationResult<Patient>.Ok(patient);
    }
}
