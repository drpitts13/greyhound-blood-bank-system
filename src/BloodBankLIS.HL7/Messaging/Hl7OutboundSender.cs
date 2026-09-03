using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.HL7.Mllp;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>
/// Transmits queued outbound ORU/DFT messages over MLLP and records the ACK.
/// <see cref="Hl7OutboundService"/> only persists the payload; this is the missing
/// SoftBank/SafeTrace transport step.
/// </summary>
public sealed class Hl7OutboundSender
{
    private readonly IRepository<Hl7MessageLog> _logs;
    private readonly IRepository<InterfaceEndpoint> _endpoints;
    private readonly IRepository<InterfaceErrorQueueItem> _errors;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public Hl7OutboundSender(
        IRepository<Hl7MessageLog> logs,
        IRepository<InterfaceEndpoint> endpoints,
        IRepository<InterfaceErrorQueueItem> errors,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _logs = logs;
        _endpoints = endpoints;
        _errors = errors;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<int> SendPendingAsync(int maxBatch = 20, CancellationToken ct = default)
    {
        var pending = await _logs.ListAsync(
            m => m.Direction == Hl7Direction.Outbound
                 && (m.Status == Hl7MessageStatus.Received || m.Status == Hl7MessageStatus.Errored),
            ct);

        var sent = 0;
        foreach (var row in pending.OrderBy(m => m.ReceivedUtc).Take(maxBatch))
        {
            var endpoint = await ResolveEndpointAsync(row, ct);
            var maxRetry = endpoint?.MaxRetryCount is > 0 ? endpoint.MaxRetryCount.Value : 5;
            if (row.RetryCount >= maxRetry)
                continue;

            var result = await SendOneAsync(row.Id, ct);
            if (result.Succeeded)
                sent++;
        }

        return sent;
    }

    public async Task<OperationResult<Hl7MessageLog>> SendOneAsync(long messageId, CancellationToken ct = default)
    {
        var row = await _logs.GetByIdAsync(messageId, ct);
        if (row is null)
            return OperationResult<Hl7MessageLog>.Fail("HL7 message not found.");

        if (row.Direction != Hl7Direction.Outbound)
            return OperationResult<Hl7MessageLog>.Fail("Only outbound messages can be sent over MLLP.");

        if (row.Status == Hl7MessageStatus.Acked)
            return OperationResult<Hl7MessageLog>.Fail("Message was already acknowledged.");

        var endpoint = await ResolveEndpointAsync(row, ct);
        if (endpoint is null)
            return OperationResult<Hl7MessageLog>.Fail("No enabled outbound MLLP endpoint is configured.");

        if (endpoint.Transport != InterfaceTransport.Mllp)
            return OperationResult<Hl7MessageLog>.Fail($"Endpoint '{endpoint.Name}' does not use MLLP transport.");

        if (string.IsNullOrWhiteSpace(endpoint.Host) || endpoint.Port is null or <= 0)
            return OperationResult<Hl7MessageLog>.Fail($"Endpoint '{endpoint.Name}' is missing host or port.");

        row.EndpointId ??= endpoint.Id;
        var timeout = TimeSpan.FromSeconds(endpoint.AckTimeoutSeconds is > 0 ? endpoint.AckTimeoutSeconds.Value : 15);
        var send = await MllpClient.SendAsync(endpoint.Host, endpoint.Port.Value, row.RawMessage, timeout, ct);

        row.RetryCount++;
        row.ProcessedUtc = _clock.UtcNow;

        if (!send.Connected || send.AckRaw is null)
        {
            row.Status = Hl7MessageStatus.Errored;
            row.ErrorDetail = send.Error ?? "No MLLP ACK received.";
            await EnqueueErrorAsync(row, "MLLP_SEND", row.ErrorDetail, ct);
        }
        else if (string.Equals(send.AckCode, AckCode.Accept, StringComparison.OrdinalIgnoreCase))
        {
            row.Status = Hl7MessageStatus.Acked;
            row.AckCode = send.AckCode;
            row.ErrorDetail = null;
        }
        else
        {
            row.Status = Hl7MessageStatus.Nacked;
            row.AckCode = send.AckCode;
            row.ErrorDetail = send.AckRaw;
            await EnqueueErrorAsync(row, "MLLP_NACK", send.AckRaw, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Hl7MessageLog>.Ok(row);
    }

    private async Task<InterfaceEndpoint?> ResolveEndpointAsync(Hl7MessageLog row, CancellationToken ct)
    {
        if (row.EndpointId is { } id)
        {
            var named = await _endpoints.GetByIdAsync(id, ct);
            if (named is not null)
                return named;
        }

        var matches = await _endpoints.ListAsync(
            e => e.IsEnabled && e.Direction == Hl7Direction.Outbound && e.Transport == InterfaceTransport.Mllp,
            ct);
        return matches.OrderBy(e => e.Name).FirstOrDefault();
    }

    private async Task EnqueueErrorAsync(Hl7MessageLog row, string type, string detail, CancellationToken ct)
    {
        var existing = await _errors.FirstOrDefaultAsync(e => e.Hl7MessageId == row.Id && !e.Resolved, ct);
        if (existing is not null)
        {
            existing.ErrorType = type;
            existing.ErrorDetail = detail;
            existing.RetryCount = row.RetryCount;
            existing.NextRetryUtc = _clock.UtcNow.AddSeconds(30);
            return;
        }

        await _errors.AddAsync(new InterfaceErrorQueueItem
        {
            Hl7MessageId = row.Id,
            ErrorType = type,
            ErrorDetail = detail,
            RetryCount = row.RetryCount,
            NextRetryUtc = _clock.UtcNow.AddSeconds(30)
        }, ct);
    }
}
