using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
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

    public EncounterService(
        IRepository<Encounter> encounters,
        IRepository<Patient> patients,
        IRepository<OrderingProvider> providers,
        OrderingProviderService orderingProviders,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _encounters = encounters;
        _patients = patients;
        _providers = providers;
        _orderingProviders = orderingProviders;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<IReadOnlyList<Encounter>> ListByPatientAsync(long patientId, CancellationToken ct = default) =>
        _encounters.ListAsync(e => e.PatientId == patientId, ct);

    public Task<Encounter?> GetAsync(long id, CancellationToken ct = default) =>
        _encounters.GetByIdAsync(id, ct);

    public async Task<OperationResult<Encounter>> CreateAsync(long patientId, CreateEncounterRequest request, CancellationToken ct = default)
    {
        if (await _patients.GetByIdAsync(patientId, ct) is null)
        {
            return OperationResult<Encounter>.Fail("Patient not found.");
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
        return OperationResult<Encounter>.Ok(encounter);
    }

    public async Task<OperationResult<Encounter>> UpdateAsync(long patientId, long encounterId, UpdateEncounterRequest request, CancellationToken ct = default)
    {
        var encounter = await _encounters.FirstOrDefaultAsync(e => e.Id == encounterId && e.PatientId == patientId, ct);
        if (encounter is null)
        {
            return OperationResult<Encounter>.Fail("Visit not found.");
        }

        var (providerId, providerName) = await ResolveAttendingProviderAsync(request.AttendingProviderId, ct);

        encounter.EncounterType = request.EncounterType;
        encounter.Status = request.Status;
        encounter.AdmitUtc = request.AdmitUtc;
        encounter.DischargeUtc = request.DischargeUtc;
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
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Encounter>.Ok(encounter);
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
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(visitNumber))
        {
            return null;
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

        if (encounter is null)
        {
            encounter = new Encounter
            {
                PatientId = patientId,
                VisitNumber = visit,
                AccountNumber = accountNumber,
                EncounterType = EncounterType.Inpatient,
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
        if (dischargeUtc is not null) encounter.DischargeUtc = dischargeUtc;
        if (currentLocation is not null) encounter.CurrentLocation = currentLocation;
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
}
