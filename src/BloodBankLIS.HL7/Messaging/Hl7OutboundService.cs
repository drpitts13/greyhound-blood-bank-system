using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>
/// Builds outbound ORU result messages and persists them to <c>HL7Messages</c>
/// (direction Outbound). The transport (MLLP sender hosted service) picks them up;
/// this service performs no network I/O, keeping it deterministic and testable.
/// </summary>
public sealed class Hl7OutboundService
{
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Hl7MessageLog> _logs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public Hl7OutboundService(
        IRepository<TestResult> results,
        IRepository<Patient> patients,
        IRepository<Hl7MessageLog> logs,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _results = results;
        _patients = patients;
        _logs = logs;
        _unitOfWork = unitOfWork;
        _clock = clock;
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
        var raw = Hl7OruBuilder.Build(patient, result, controlId, now);

        var log = new Hl7MessageLog
        {
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
}
