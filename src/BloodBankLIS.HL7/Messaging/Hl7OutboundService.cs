using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>
/// Builds outbound ORU result messages and DFT billing messages and persists them
/// to <c>HL7Messages</c> (direction Outbound). The transport (MLLP sender hosted
/// service) picks them up; this service performs no network I/O.
/// </summary>
public sealed class Hl7OutboundService : IBillingInterfacePublisher
{
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Hl7MessageLog> _logs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IRepository<InterfaceEndpoint>? _endpoints;
    private readonly IInterfaceFieldMappingRepository? _mappings;

    public Hl7OutboundService(
        IRepository<TestResult> results,
        IRepository<Patient> patients,
        IRepository<Hl7MessageLog> logs,
        IUnitOfWork unitOfWork,
        IClock clock,
        IRepository<InterfaceEndpoint>? endpoints = null,
        IInterfaceFieldMappingRepository? mappings = null)
    {
        _results = results;
        _patients = patients;
        _logs = logs;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _endpoints = endpoints;
        _mappings = mappings;
    }

    public async Task<OperationResult<Hl7MessageLog>> QueueResultMessageAsync(long resultId, CancellationToken ct = default)
    {
        var result = await _results.GetByIdAsync(resultId, ct);
        if (result is null)
        {
            return OperationResult<Hl7MessageLog>.Fail($"Result {resultId} not found.");
        }

        if (result.Status != ResultStatus.Verified)
        {
            return OperationResult<Hl7MessageLog>.Fail("Only verified results are released over HL7.");
        }

        var patient = await _patients.GetByIdAsync(result.PatientId, ct);
        if (patient is null)
        {
            return OperationResult<Hl7MessageLog>.Fail($"Patient {result.PatientId} not found.");
        }

        var now = _clock.UtcNow;
        var controlId = $"OUT{now:yyyyMMddHHmmssfff}";
        var resolved = await ResolveOutboundAsync(InterfaceType.Results, ct);
        var raw = Hl7OruBuilder.Build(patient, result, controlId, now, resolved.Identity, map: resolved.Map);

        var log = new Hl7MessageLog
        {
            EndpointId = resolved.EndpointId,
            Direction = Hl7Direction.Outbound,
            MessageType = "ORU",
            TriggerEvent = "R01",
            MessageControlId = controlId,
            RawMessage = raw,
            Status = Hl7MessageStatus.Received,
            ReceivedUtc = now
        };
        await _logs.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return OperationResult<Hl7MessageLog>.Ok(log);
    }

    public async Task<long?> PublishChargeAsync(BillingEvent billingEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(billingEvent);

        Patient? patient = null;
        if (billingEvent.PatientId is { } patientId)
        {
            patient = await _patients.GetByIdAsync(patientId, ct);
        }

        var now = _clock.UtcNow;
        var controlId = $"DFT{now:yyyyMMddHHmmssfff}{billingEvent.Id}";
        var resolved = await ResolveOutboundAsync(InterfaceType.Billing, ct);
        var raw = Hl7DftBuilder.Build(patient, billingEvent, controlId, now, resolved.Identity, map: resolved.Map);

        var log = new Hl7MessageLog
        {
            EndpointId = resolved.EndpointId,
            Direction = Hl7Direction.Outbound,
            MessageType = "DFT",
            TriggerEvent = "P03",
            MessageControlId = controlId,
            RawMessage = raw,
            Status = Hl7MessageStatus.Received,
            ReceivedUtc = now
        };
        await _logs.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return log.Id;
    }

    private async Task<(Hl7FieldMap Map, Hl7OutboundIdentity Identity, long? EndpointId)> ResolveOutboundAsync(
        InterfaceType type,
        CancellationToken ct)
    {
        var fallback = (Hl7FieldMap.Default(type, Hl7Direction.Outbound), new Hl7OutboundIdentity(), (long?)null);
        if (_endpoints is null)
        {
            return fallback;
        }

        var matches = await _endpoints.ListAsync(
            e => e.IsEnabled && e.Direction == Hl7Direction.Outbound && e.InterfaceType == type, ct);
        var endpoint = matches.OrderBy(e => e.Name).FirstOrDefault();
        if (endpoint is null)
        {
            return fallback;
        }

        if (_mappings is not null)
        {
            var rows = await _mappings.ListAsync(m => m.EndpointId == endpoint.Id, ct);
            endpoint.FieldMappings = rows.ToList();
        }

        return (Hl7FieldMap.From(endpoint), Hl7OutboundIdentity.From(endpoint), endpoint.Id);
    }
}
