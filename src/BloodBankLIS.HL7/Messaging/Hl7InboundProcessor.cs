using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>Outcome of processing one inbound message: the response to send and the persisted log.</summary>
public sealed record Hl7ProcessResult(string AckMessage, string AckCode, Hl7MessageLog Log)
{
    public bool Accepted => AckCode == Parsing.AckCode.Accept;
}

/// <summary>
/// The inbound HL7 pipeline (docs/hl7-design.md section 3): persist the raw message,
/// parse, map, execute the matching Application action, and build an ACK/NAK — all in
/// one transaction. Clinical actions run through the same domain logic as the API, so
/// safety rules always apply. Idempotent on MSH-10 control id so replays do not
/// duplicate patients or orders.
/// </summary>
public sealed class Hl7InboundProcessor
{
    private readonly IRepository<Hl7MessageLog> _logs;
    private readonly IRepository<InterfaceErrorQueueItem> _errors;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderingLocation> _locationRepo;
    private readonly EncounterService _encounters;
    private readonly OrderService _ordersService;
    private readonly OrderingProviderService _orderingProviders;
    private readonly OrderingLocationService _orderingLocationCatalog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IRepository<InterfaceEndpoint>? _endpoints;
    private readonly IInterfaceFieldMappingRepository? _mappings;
    private readonly IInterfaceValueTranslationRepository? _translations;
    private readonly InterfaceTransfusionService? _bpam;
    private readonly PatientMergeService? _merges;

    public Hl7InboundProcessor(
        IRepository<Hl7MessageLog> logs,
        IRepository<InterfaceErrorQueueItem> errors,
        IRepository<Patient> patients,
        IRepository<Order> orders,
        IRepository<OrderingLocation> locationRepo,
        EncounterService encounters,
        OrderService ordersService,
        OrderingProviderService orderingProviders,
        OrderingLocationService orderingLocationCatalog,
        IUnitOfWork unitOfWork,
        IClock clock,
        IRepository<InterfaceEndpoint>? endpoints = null,
        IInterfaceFieldMappingRepository? mappings = null,
        InterfaceTransfusionService? bpam = null,
        IInterfaceValueTranslationRepository? translations = null,
        PatientMergeService? merges = null)
    {
        _logs = logs;
        _errors = errors;
        _patients = patients;
        _orders = orders;
        _locationRepo = locationRepo;
        _encounters = encounters;
        _ordersService = ordersService;
        _orderingProviders = orderingProviders;
        _orderingLocationCatalog = orderingLocationCatalog;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _endpoints = endpoints;
        _mappings = mappings;
        _bpam = bpam;
        _translations = translations;
        _merges = merges;
    }

    /// <summary>
    /// Re-submits a previously stored inbound message through the same pipeline. The
    /// replay is recorded as a new <see cref="Hl7MessageLog"/> with status Replayed;
    /// idempotency on the control id prevents duplicate clinical effects.
    /// </summary>
    public async Task<Hl7ProcessResult?> ReplayAsync(long messageId, CancellationToken ct = default)
    {
        var original = await _logs.GetByIdAsync(messageId, ct);
        if (original is null || original.Direction != Hl7Direction.Inbound)
        {
            return null;
        }

        return await ProcessAsync(original.RawMessage, original.EndpointId, isReplay: true, ct);
    }

    public async Task<Hl7ProcessResult> ProcessAsync(string rawMessage, long? endpointId = null, bool isReplay = false, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var ackControlId = now.ToString("yyyyMMddHHmmssfff");

        if (!Hl7Parser.TryParse(rawMessage, out var message, out var parseError))
        {
            var errorLog = new Hl7MessageLog
            {
                EndpointId = endpointId,
                Direction = Hl7Direction.Inbound,
                MessageType = "UNKNOWN",
                MessageControlId = string.Empty,
                RawMessage = rawMessage ?? string.Empty,
                Status = Hl7MessageStatus.Errored,
                ReceivedUtc = now,
                AckCode = AckCode.Reject,
                ErrorDetail = parseError
            };
            await _logs.AddAsync(errorLog, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var nak = Hl7AckBuilder.BuildParseNak(null, parseError ?? "Parse error", ackControlId, now);
            return new Hl7ProcessResult(nak, AckCode.Reject, errorLog);
        }

        var log = new Hl7MessageLog
        {
            EndpointId = endpointId,
            Direction = Hl7Direction.Inbound,
            MessageType = message!.MessageType,
            TriggerEvent = message.TriggerEvent,
            MessageControlId = message.MessageControlId,
            RawMessage = rawMessage!,
            Status = isReplay ? Hl7MessageStatus.Replayed : Hl7MessageStatus.Received,
            ReceivedUtc = now
        };
        await _logs.AddAsync(log, ct);

        var alreadyProcessed = await _logs.AnyAsync(
            l => l.MessageControlId == message.MessageControlId
                 && l.Direction == Hl7Direction.Inbound
                 && l.Id != log.Id
                 && (l.Status == Hl7MessageStatus.Processed || l.Status == Hl7MessageStatus.Acked), ct);

        if (alreadyProcessed && !isReplay)
        {
            log.Status = Hl7MessageStatus.Processed;
            log.AckCode = AckCode.Accept;
            log.ProcessedUtc = now;
            log.ErrorDetail = "Duplicate control id; acknowledged without reprocessing.";
            await _unitOfWork.SaveChangesAsync(ct);
            return new Hl7ProcessResult(
                Hl7AckBuilder.BuildAck(message, AckCode.Accept, "Duplicate; not reprocessed.", ackControlId, now),
                AckCode.Accept,
                log);
        }

        try
        {
            var (map, resolvedId, enabled) = await ResolveInboundAsync(message.MessageType, endpointId, ct);
            log.EndpointId ??= resolvedId;
            if (enabled)
            {
                map.Translator = await LoadTranslatorAsync(ct);
            }

            var text = await DispatchAsync(message, map, ct);
            log.Status = isReplay ? Hl7MessageStatus.Replayed : Hl7MessageStatus.Processed;
            log.AckCode = AckCode.Accept;
            log.ProcessedUtc = now;
            await _unitOfWork.SaveChangesAsync(ct);
            return new Hl7ProcessResult(
                Hl7AckBuilder.BuildAck(message, AckCode.Accept, text, ackControlId, now),
                AckCode.Accept,
                log);
        }
        catch (Hl7MappingException ex)
        {
            log.Status = Hl7MessageStatus.Errored;
            log.AckCode = AckCode.ApplicationError;
            log.ErrorDetail = ex.Message;
            log.ProcessedUtc = now;
            await _errors.AddAsync(new InterfaceErrorQueueItem
            {
                Hl7Message = log,
                ErrorType = "Mapping",
                ErrorDetail = ex.Message,
                NextRetryUtc = now.AddMinutes(5),
                RetryCount = 0,
                Resolved = false
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return new Hl7ProcessResult(
                Hl7AckBuilder.BuildAck(message, AckCode.ApplicationError, ex.Message, ackControlId, now),
                AckCode.ApplicationError,
                log);
        }
    }

    private async Task<string> DispatchAsync(Hl7Message message, Hl7FieldMap map, CancellationToken ct) =>
        message.MessageType switch
        {
            "ADT" => await HandleAdtAsync(message, map, ct),
            "ORM" or "OML" => await HandleOrmAsync(message, map, ct),
            "RAS" or "BPS" => await HandleBpamAsync(message, map, ct),
            _ => throw new Hl7MappingException($"Unsupported message type '{message.MessageType}'.")
        };

    private async Task<string> HandleAdtAsync(Hl7Message message, Hl7FieldMap map, CancellationToken ct)
    {
        var data = Hl7AdtMapper.Map(message, map);
        if (string.IsNullOrWhiteSpace(data.Mrn))
        {
            throw new Hl7MappingException("ADT message has no patient identifier (PID-3).");
        }

        if (data.TriggerEvent is "A18" or "A40")
        {
            return await HandleAdtMergeAsync(data, ct);
        }

        var patient = _merges is not null
            ? await _merges.FindByMrnAsync(data.Mrn, followMerge: true, ct)
            : await _patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == data.Mrn, ct);
        patient = await PatientMergeFollow.ResolveClinicalRecordAsync(_patients, patient, ct);
        if (patient is not null)
        {
            var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
            if (clinical.Severity == RuleSeverity.HardStop)
            {
                throw new Hl7MappingException(clinical.Message);
            }
        }

        var created = false;
        if (patient is null)
        {
            patient = new Patient
            {
                MedicalRecordNumber = data.Mrn,
                LastName = data.LastName,
                FirstName = data.FirstName,
                MiddleName = data.MiddleName,
                DateOfBirth = data.DateOfBirth ?? default,
                Sex = data.Sex
            };
            await _patients.AddAsync(patient, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            created = true;
        }
        else
        {
            patient.LastName = data.LastName;
            patient.FirstName = data.FirstName;
            patient.MiddleName = data.MiddleName;
            if (data.DateOfBirth is not null) patient.DateOfBirth = data.DateOfBirth.Value;
            patient.Sex = data.Sex;
            _patients.Update(patient);
        }

        var visitNote = await _encounters.UpsertVisitFromHl7Async(
            patient.Id,
            data.VisitNumber ?? string.Empty,
            data.AccountNumber,
            data.AdmitUtc,
            data.DischargeUtc,
            data.CurrentLocation,
            data.AttendingProviderId,
            data.AttendingProviderName,
            data.TriggerEvent,
            ct);

        if (visitNote is not null)
        {
            return created
                ? $"Patient {data.Mrn} created. {visitNote}"
                : $"Patient {data.Mrn} updated. {visitNote}";
        }

        return created ? $"Patient {data.Mrn} created." : $"Patient {data.Mrn} updated.";
    }

    private async Task<string> HandleAdtMergeAsync(Hl7PatientData data, CancellationToken ct)
    {
        if (_merges is null)
        {
            throw new Hl7MappingException("Patient merge is not configured for this inbound processor.");
        }

        if (string.IsNullOrWhiteSpace(data.PriorMrn))
        {
            throw new Hl7MappingException("ADT merge message has no prior patient identifier (MRG-1).");
        }

        if (string.Equals(data.Mrn, data.PriorMrn, StringComparison.OrdinalIgnoreCase))
        {
            throw new Hl7MappingException("ADT merge cannot use the same MRN for survivor and prior records.");
        }

        var survivor = await _merges.FindByMrnAsync(data.Mrn, followMerge: true, ct);
        if (survivor is null)
        {
            survivor = new Patient
            {
                MedicalRecordNumber = data.Mrn,
                LastName = data.LastName,
                FirstName = data.FirstName,
                MiddleName = data.MiddleName,
                DateOfBirth = data.DateOfBirth ?? default,
                Sex = data.Sex
            };
            await _patients.AddAsync(survivor, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        else
        {
            survivor.LastName = data.LastName;
            survivor.FirstName = data.FirstName;
            survivor.MiddleName = data.MiddleName;
            if (data.DateOfBirth is not null) survivor.DateOfBirth = data.DateOfBirth.Value;
            survivor.Sex = data.Sex;
            _patients.Update(survivor);
        }

        var duplicate = await _merges.FindByMrnAsync(data.PriorMrn, followMerge: false, ct);
        if (duplicate is null)
        {
            throw new Hl7MappingException($"Prior patient '{data.PriorMrn}' was not found; merge was not applied.");
        }

        var merged = await _merges.MergeFromHl7Async(
            survivor.Id,
            duplicate.Id,
            $"HL7 ADT^{data.TriggerEvent} merge of {data.PriorMrn} into {data.Mrn}.",
            ct);
        if (!merged.Succeeded)
        {
            throw new Hl7MappingException(merged.Error ?? "Patient merge failed.");
        }

        var visitNote = await _encounters.UpsertVisitFromHl7Async(
            merged.Value!.Id,
            data.VisitNumber ?? string.Empty,
            data.AccountNumber,
            data.AdmitUtc,
            data.DischargeUtc,
            data.CurrentLocation,
            data.AttendingProviderId,
            data.AttendingProviderName,
            data.TriggerEvent,
            ct);

        return visitNote is null
            ? $"Patient {data.PriorMrn} merged into {data.Mrn}."
            : $"Patient {data.PriorMrn} merged into {data.Mrn}. {visitNote}";
    }

    private async Task<string> HandleOrmAsync(Hl7Message message, Hl7FieldMap map, CancellationToken ct)
    {
        var data = Hl7OrmMapper.Map(message, map);
        if (string.IsNullOrWhiteSpace(data.PlacerOrderId))
        {
            throw new Hl7MappingException("Order message has no placer order id (ORC-2/OBR-2).");
        }

        var patient = _merges is not null
            ? await _merges.FindByMrnAsync(data.Mrn, followMerge: true, ct)
            : await _patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == data.Mrn, ct);
        patient = await PatientMergeFollow.ResolveClinicalRecordAsync(_patients, patient, ct);
        if (patient is null)
        {
            throw new Hl7MappingException($"No patient found for MRN '{data.Mrn}'; order cannot be created.");
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        if (clinical.Severity == RuleSeverity.HardStop)
        {
            throw new Hl7MappingException(clinical.Message);
        }

        var existing = await _orders.FirstOrDefaultAsync(o => o.OrderNumber == data.PlacerOrderId, ct);

        if (!OrderControlRule.IsNewOrder(data.OrderControl))
        {
            if (existing is null)
            {
                throw new Hl7MappingException($"Order control {data.OrderControl} for unknown order '{data.PlacerOrderId}'.");
            }

            var applied = OrderControlRule.Apply(existing, data.OrderControl, reason: null);
            if (applied.Severity == RuleSeverity.HardStop)
            {
                throw new Hl7MappingException(applied.Message);
            }

            _orders.Update(existing);
            return $"Order {data.PlacerOrderId}: {applied.Message}";
        }

        if (existing is not null)
        {
            return $"Order {data.PlacerOrderId} already exists; not duplicated.";
        }

        var encounter = await _encounters.EnsureEncounterForHl7OrderAsync(patient.Id, data.VisitNumber, ct);
        var location = await _orderingLocationCatalog.EnsureFromHl7Async(
                data.OrderingLocationCode,
                name: data.OrderingLocationCode,
                department: null,
                ct)
            ?? await _locationRepo.FirstOrDefaultAsync(l => l.IsActive, ct)
            ?? await _locationRepo.FirstOrDefaultAsync(l => l.Code == "LEGACY", ct)
            ?? throw new Hl7MappingException("No ordering location configured; cannot create HL7 order.");

        var provider = await _orderingProviders.EnsureFromHl7Async(
            data.OrderingProviderId,
            data.OrderingProviderName,
            specialty: null,
            location: null,
            "HL7",
            ct);

        var testCode = string.IsNullOrWhiteSpace(data.TestCode)
            ? data.OrderType.ToString()
            : data.TestCode.Trim().ToUpperInvariant();
        var createResult = await _ordersService.CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id,
            location.Id,
            data.PlacerOrderId,
            [new OrderLineInputDto(OrderCategory.Test, testCode, null)],
            OrderPriority.Routine,
            _clock.UtcNow,
            provider?.Id,
            OrderSource.Hl7,
            "HL7",
            null), ct);

        if (!createResult.Succeeded)
        {
            throw new Hl7MappingException(createResult.Error ?? "Failed to create HL7 order.");
        }

        return $"Order {data.PlacerOrderId} created.";
    }

    private async Task<string> HandleBpamAsync(Hl7Message message, Hl7FieldMap map, CancellationToken ct)
    {
        if (_bpam is null)
        {
            throw new Hl7MappingException("BPAM documentation is not configured.");
        }

        var data = Hl7BpamMapper.Map(message, map);
        try
        {
            return await _bpam.DocumentAsync(new InterfaceTransfusionRequest(
                data.Mrn,
                data.UnitNumber,
                data.Din,
                data.StartUtc,
                data.StopUtc,
                data.VolumeTransfused,
                data.Location,
                data.Transfusionist,
                data.ReactionSuspected), ct);
        }
        catch (InvalidOperationException ex)
        {
            throw new Hl7MappingException(ex.Message);
        }
    }

    private async Task<(Hl7FieldMap Map, long? EndpointId, bool Enabled)> ResolveInboundAsync(
        string messageType,
        long? requestedId,
        CancellationToken ct)
    {
        var fallbackType = messageType switch
        {
            "ADT" => InterfaceType.Adt,
            "ORM" or "OML" => InterfaceType.Orders,
            "RAS" or "BPS" => InterfaceType.Bpam,
            _ => InterfaceType.Adt
        };

        if (_endpoints is null)
        {
            return (Hl7FieldMap.Default(fallbackType, Hl7Direction.Inbound), requestedId, false);
        }

        InterfaceEndpoint? endpoint = null;
        if (requestedId is long id)
        {
            endpoint = await _endpoints.GetByIdAsync(id, ct);
        }

        if (endpoint is null)
        {
            var inbound = await _endpoints.ListAsync(
                e => e.IsEnabled && e.Direction == Hl7Direction.Inbound, ct);
            endpoint = inbound
                .Where(e => InterfaceTypeDefaults.SupportsMessageType(e.InterfaceType, messageType)
                    || e.MessageTypes.Contains(messageType, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Name)
                .FirstOrDefault();
        }

        if (endpoint is null)
        {
            return (Hl7FieldMap.Default(fallbackType, Hl7Direction.Inbound), requestedId, false);
        }

        if (_mappings is not null)
        {
            var rows = await _mappings.ListAsync(m => m.EndpointId == endpoint.Id, ct);
            endpoint.FieldMappings = rows.ToList();
        }

        return (Hl7FieldMap.From(endpoint), endpoint.Id, endpoint.IsEnabled);
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

/// <summary>An application-level mapping/execution failure that yields an AE (retryable) NAK.</summary>
public sealed class Hl7MappingException(string message) : Exception(message);
