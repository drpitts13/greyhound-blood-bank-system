using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
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
/// Every send first claims the row with a conditional UPDATE (status
/// <see cref="Hl7MessageStatus.Sending"/>), so the background loop and a manual send
/// never transmit the same message twice. A claim left behind by a crashed sender is
/// taken over after <see cref="ClaimTimeout"/>.
/// </summary>
public sealed class Hl7OutboundSender
{
    private readonly IRepository<Hl7MessageLog> _logs;
    private readonly IRepository<InterfaceEndpoint> _endpoints;
    private readonly IRepository<InterfaceErrorQueueItem> _errors;
    private readonly IHl7OutboundClaimStore _claims;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public Hl7OutboundSender(
        IRepository<Hl7MessageLog> logs,
        IRepository<InterfaceEndpoint> endpoints,
        IRepository<InterfaceErrorQueueItem> errors,
        IHl7OutboundClaimStore claims,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _logs = logs;
        _endpoints = endpoints;
        _errors = errors;
        _claims = claims;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public const int DefaultMaxRetries = 5;

    /// <summary>
    /// How long a <see cref="Hl7MessageStatus.Sending"/> claim is honoured before another
    /// sender may take it over. Comfortably longer than any MLLP ACK timeout.
    /// </summary>
    public static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(5);

    public static int EffectiveMaxRetries(InterfaceEndpoint? endpoint) =>
        endpoint?.MaxRetryCount is > 0 ? endpoint.MaxRetryCount.Value : DefaultMaxRetries;

    public static bool RetriesExhausted(Hl7Direction direction, int retryCount, InterfaceEndpoint? endpoint) =>
        direction == Hl7Direction.Outbound && retryCount >= EffectiveMaxRetries(endpoint);

    public async Task<int> SendPendingAsync(int maxBatch = 20, CancellationToken ct = default)
    {
        var staleBefore = _clock.UtcNow - ClaimTimeout;
        var pending = await _logs.ListAsync(
            m => m.Direction == Hl7Direction.Outbound
                 && (m.Status == Hl7MessageStatus.Received
                     || m.Status == Hl7MessageStatus.Errored
                     || (m.Status == Hl7MessageStatus.Sending
                         && (m.ClaimedUtc == null || m.ClaimedUtc < staleBefore))),
            ct);

        var sent = 0;
        foreach (var row in pending.OrderBy(m => m.ReceivedUtc).Take(maxBatch))
        {
            var endpoint = await ResolveEndpointAsync(row, ct);
            if (RetriesExhausted(row.Direction, row.RetryCount, endpoint))
                continue;

            var result = await SendOneAsync(row.Id, ct);
            if (result.Succeeded)
                sent++;
        }

        return sent;
    }

    public async Task<OperationResult<Hl7MessageLog>> SendOneAsync(long messageId, CancellationToken ct = default)
    {
        // Validate against an untracked snapshot; the tracked row comes from the claim.
        var snapshot = (await _logs.ListAsync(m => m.Id == messageId, ct)).FirstOrDefault();
        if (snapshot is null)
            return OperationResult<Hl7MessageLog>.Fail("HL7 message not found.");

        var now = _clock.UtcNow;
        var staleBefore = now - ClaimTimeout;
        if (snapshot.Status == Hl7MessageStatus.Sending
            && snapshot.ClaimedUtc is { } claimedUtc && claimedUtc >= staleBefore)
        {
            return OperationResult<Hl7MessageLog>.Fail("Message is already being sent.");
        }

        var check = await ValidateSendAsync(snapshot, ct);
        if (!check.Succeeded)
            return OperationResult<Hl7MessageLog>.Fail(check.Error!);

        var endpoint = check.Value!;
        var row = await _claims.TryClaimAsync(messageId, snapshot.Status, now, staleBefore, ct);
        if (row is null)
            return OperationResult<Hl7MessageLog>.Fail("Message is already being sent or was sent by another process.");

        // Another sender may have retried it between the snapshot and the claim.
        if (RetriesExhausted(row.Direction, row.RetryCount, endpoint))
        {
            row.Status = snapshot.Status;
            await _unitOfWork.SaveChangesAsync(ct);
            return OperationResult<Hl7MessageLog>.Fail(
                $"Outbound retries are exhausted ({row.RetryCount} of {EffectiveMaxRetries(endpoint)}).");
        }

        row.EndpointId ??= endpoint.Id;
        row.RetryCount++;
        row.ProcessedUtc = now;

        if (endpoint.Transport == InterfaceTransport.File)
        {
            await ApplyFileSendAsync(row, endpoint, ct);
        }
        else
        {
            await ApplyMllpSendAsync(row, endpoint, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Hl7MessageLog>.Ok(row);
    }

    private async Task<OperationResult<InterfaceEndpoint>> ValidateSendAsync(Hl7MessageLog row, CancellationToken ct)
    {
        if (row.Direction != Hl7Direction.Outbound)
            return OperationResult<InterfaceEndpoint>.Fail("Only outbound messages can be sent.");

        if (row.Status == Hl7MessageStatus.Acked)
            return OperationResult<InterfaceEndpoint>.Fail("Message was already acknowledged.");

        if (row.ErrorDetail == BillingService.UnsentDftWithheldDetail)
        {
            return OperationResult<InterfaceEndpoint>.Fail(
                "This DFT was withheld because the charge was cancelled before it was sent.");
        }

        var endpoint = await ResolveEndpointAsync(row, ct);
        if (endpoint is null)
            return OperationResult<InterfaceEndpoint>.Fail("No enabled outbound endpoint is configured.");

        if (RetriesExhausted(row.Direction, row.RetryCount, endpoint))
        {
            return OperationResult<InterfaceEndpoint>.Fail(
                $"Outbound retries are exhausted ({row.RetryCount} of {EffectiveMaxRetries(endpoint)}).");
        }

        if (endpoint.Transport is not (InterfaceTransport.File or InterfaceTransport.Mllp))
            return OperationResult<InterfaceEndpoint>.Fail($"Endpoint '{endpoint.Name}' uses an unsupported transport.");

        return OperationResult<InterfaceEndpoint>.Ok(endpoint);
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
            await ResolveErrorsAsync(row.Id, ct);
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
            await ResolveErrorsAsync(row.Id, ct);
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

    private async Task ResolveErrorsAsync(long messageId, CancellationToken ct)
    {
        var open = await _errors.ListAsync(e => e.Hl7MessageId == messageId && !e.Resolved, ct);
        if (open.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        foreach (var item in open)
        {
            item.Resolved = true;
            item.ResolvedBy = "outbound";
            item.ResolvedUtc = now;
            _errors.Update(item);
        }
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
