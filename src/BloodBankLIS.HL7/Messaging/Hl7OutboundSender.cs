using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Mllp;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>
/// Transmits queued outbound ORU/DFT messages over MLLP or a file-drop folder
/// and records the ACK. <see cref="Hl7OutboundService"/> only persists the payload.
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
            return OperationResult<Hl7MessageLog>.Fail("Only outbound messages can be sent.");

        if (row.Status == Hl7MessageStatus.Acked)
            return OperationResult<Hl7MessageLog>.Fail("Message was already acknowledged.");

        var endpoint = await ResolveEndpointAsync(row, ct);
        if (endpoint is null)
            return OperationResult<Hl7MessageLog>.Fail("No enabled outbound endpoint is configured.");

        row.EndpointId ??= endpoint.Id;
        row.RetryCount++;
        row.ProcessedUtc = _clock.UtcNow;

        if (endpoint.Transport == InterfaceTransport.File)
        {
            await ApplyFileSendAsync(row, endpoint, ct);
        }
        else if (endpoint.Transport == InterfaceTransport.Mllp)
        {
            await ApplyMllpSendAsync(row, endpoint, ct);
        }
        else
        {
            return OperationResult<Hl7MessageLog>.Fail($"Endpoint '{endpoint.Name}' uses an unsupported transport.");
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Hl7MessageLog>.Ok(row);
    }

    private async Task ApplyFileSendAsync(Hl7MessageLog row, InterfaceEndpoint endpoint, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Path))
        {
            row.Status = Hl7MessageStatus.Errored;
            row.ErrorDetail = $"Endpoint '{endpoint.Name}' is missing a file-drop path.";
            await EnqueueErrorAsync(row, "FILE_SEND", row.ErrorDetail, ct);
            return;
        }

        try
        {
            var fileName = Hl7FileDropLayout.OutboundFileName(row.MessageControlId, _clock.UtcNow);
            Hl7FileDropIO.WriteOutbound(endpoint.Path.Trim(), fileName, row.RawMessage);
            row.Status = Hl7MessageStatus.Acked;
            row.AckCode = AckCode.Accept;
            row.ErrorDetail = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            row.Status = Hl7MessageStatus.Errored;
            row.ErrorDetail = ex.Message;
            await EnqueueErrorAsync(row, "FILE_SEND", row.ErrorDetail, ct);
        }
    }

    private async Task ApplyMllpSendAsync(Hl7MessageLog row, InterfaceEndpoint endpoint, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Host) || endpoint.Port is null or <= 0)
        {
            row.Status = Hl7MessageStatus.Errored;
            row.ErrorDetail = $"Endpoint '{endpoint.Name}' is missing host or port.";
            await EnqueueErrorAsync(row, "MLLP_SEND", row.ErrorDetail, ct);
            return;
        }

        var timeout = TimeSpan.FromSeconds(endpoint.AckTimeoutSeconds is > 0 ? endpoint.AckTimeoutSeconds.Value : 15);
        var send = await MllpClient.SendAsync(endpoint.Host, endpoint.Port.Value, row.RawMessage, timeout, ct);

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
            e => e.IsEnabled && e.Direction == Hl7Direction.Outbound, ct);
        return matches
            .Where(CanSend)
            .OrderBy(e => e.Transport == InterfaceTransport.Mllp ? 0 : 1)
            .ThenBy(e => e.Name)
            .FirstOrDefault();
    }

    private static bool CanSend(InterfaceEndpoint e) => e.Transport switch
    {
        InterfaceTransport.Mllp => !string.IsNullOrWhiteSpace(e.Host) && e.Port is > 0,
        InterfaceTransport.File => !string.IsNullOrWhiteSpace(e.Path),
        _ => false
    };

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
