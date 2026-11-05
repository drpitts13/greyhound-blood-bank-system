using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>
/// Builds outbound ORU result, DFT billing, and BPS blood product dispense messages and persists them
/// to <c>HL7Messages</c> (direction Outbound). The transport (MLLP sender hosted
/// service) picks them up; this service performs no network I/O.
/// </summary>
public sealed class Hl7OutboundService : IBillingInterfacePublisher, IResultReleasePublisher, IProductDispensePublisher
{
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Hl7MessageLog> _logs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IRepository<InterfaceEndpoint>? _endpoints;
    private readonly IInterfaceFieldMappingRepository? _mappings;
    private readonly IInterfaceValueTranslationRepository? _translations;
    private readonly IRepository<Issue>? _issues;
    private readonly IRepository<BloodUnit>? _units;
    private readonly IRepository<ProductType>? _productTypes;
    private readonly IRepository<Order>? _orders;

    public Hl7OutboundService(
        IRepository<TestResult> results,
        IRepository<Patient> patients,
        IRepository<Hl7MessageLog> logs,
        IUnitOfWork unitOfWork,
        IClock clock,
        IRepository<InterfaceEndpoint>? endpoints = null,
        IInterfaceFieldMappingRepository? mappings = null,
        IInterfaceValueTranslationRepository? translations = null,
        IRepository<Issue>? issues = null,
        IRepository<BloodUnit>? units = null,
        IRepository<ProductType>? productTypes = null,
        IRepository<Order>? orders = null)
    {
        _issues = issues;
        _units = units;
        _productTypes = productTypes;
        _orders = orders;
        _results = results;
        _patients = patients;
        _logs = logs;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _endpoints = endpoints;
        _mappings = mappings;
        _translations = translations;
    }

    public async Task<OperationResult<Hl7MessageLog>> QueueResultMessageAsync(long resultId, CancellationToken ct = default)
    {
        var result = await _results.GetByIdAsync(resultId, ct);
        if (result is null)
        {
            return OperationResult<Hl7MessageLog>.Fail($"Result {resultId} not found.");
        }

        // A verified result goes as final or corrected; a verified result later invalidated goes as wrong, so the EHR
        // stops showing it. Unverified results are never sent.
        if (ResultReleaseRule.ObxStatus(result.Status, result.Version, result.VerifiedUtc is not null) is null)
        {
            return OperationResult<Hl7MessageLog>.Fail("Only verified results, or verified results since invalidated, are sent over HL7.");
        }

        var patient = await _patients.GetByIdAsync(result.PatientId, ct);
        if (patient is null)
        {
            return OperationResult<Hl7MessageLog>.Fail($"Patient {result.PatientId} not found.");
        }

        var now = _clock.UtcNow;
        var controlId = $"OUT{now:yyyyMMddHHmmssfff}";
        var resolved = await ResolveOutboundAsync(InterfaceType.Results, ct);
        var translator = await LoadTranslatorAsync(ct);
        var raw = Hl7OruBuilder.Build(patient, result, controlId, now, resolved.Identity, map: resolved.Map, translator: translator);

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

    /// <summary>
    /// OCD-049: a correction (C) or a withdrawal (W) is queued without anyone asking, but only when an outbound
    /// results interface is enabled, so no message is left with nowhere to go.
    /// </summary>
    public async Task<long?> PublishResultChangeAsync(long resultId, CancellationToken ct = default)
    {
        var result = await _results.GetByIdAsync(resultId, ct);
        if (result is null
            || !ResultReleaseRule.SentAutomatically(ResultReleaseRule.ObxStatus(result.Status, result.Version, result.VerifiedUtc is not null)))
        {
            return null;
        }

        if (_endpoints is null
            || !await _endpoints.AnyAsync(e => e.IsEnabled && e.Direction == Hl7Direction.Outbound && e.InterfaceType == InterfaceType.Results, ct))
        {
            return null;
        }

        var queued = await QueueResultMessageAsync(resultId, ct);
        return queued.Succeeded ? queued.Value!.Id : null;
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
        var translator = await LoadTranslatorAsync(ct);
        var raw = Hl7DftBuilder.Build(patient, billingEvent, controlId, now, resolved.Identity, map: resolved.Map, translator: translator);

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

    /// <summary>
    /// BPS^O29 for a unit dispensed to, or returned from, a patient, so bedside administration in the EHR can match the
    /// unit it scans. Queued only when an outbound blood product (BPAM) interface is enabled.
    /// </summary>
    public async Task<long?> PublishDispenseAsync(long issueId, ProductDispenseEvent dispenseEvent, CancellationToken ct = default)
    {
        if (_endpoints is null || _issues is null || _units is null
            || !await _endpoints.AnyAsync(e => e.IsEnabled && e.Direction == Hl7Direction.Outbound && e.InterfaceType == InterfaceType.Bpam, ct))
        {
            return null;
        }

        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return null;
        }

        var unit = await _units.GetByIdAsync(issue.BloodProductId, ct);
        var patient = await _patients.GetByIdAsync(issue.PatientId, ct);
        if (unit is null || patient is null)
        {
            return null;
        }

        var product = _productTypes is null ? null : await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        var order = _orders is null || issue.OrderId is not long orderId ? null : await _orders.GetByIdAsync(orderId, ct);
        var now = _clock.UtcNow;
        var statusUtc = dispenseEvent == ProductDispenseEvent.Dispensed ? issue.IssuedUtc : now;
        var controlId = $"BPS{now:yyyyMMddHHmmssfff}{issue.Id}";
        var resolved = await ResolveOutboundAsync(InterfaceType.Bpam, ct);
        var translator = await LoadTranslatorAsync(ct);
        var raw = Hl7BpsBuilder.Build(patient, issue, unit, product, order, dispenseEvent, statusUtc, controlId, now,
            resolved.Identity, map: resolved.Map, translator: translator);

        var log = new Hl7MessageLog
        {
            EndpointId = resolved.EndpointId,
            Direction = Hl7Direction.Outbound,
            MessageType = "BPS",
            TriggerEvent = "O29",
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

    private async Task<InterfaceValueTranslator> LoadTranslatorAsync(CancellationToken ct)
    {
        if (_translations is null)
        {
            return InterfaceValueTranslator.Empty;
        }

        var rows = await _translations.ListAsync(ct);
        return InterfaceValueTranslator.From(rows);
    }
}
