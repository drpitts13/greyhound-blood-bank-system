using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.HL7.Messaging;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>Read model for a persisted HL7 message (raw text excluded from list views).</summary>
public sealed record Hl7MessageDto(
    long Id,
    Hl7Direction Direction,
    string MessageType,
    string? TriggerEvent,
    string MessageControlId,
    Hl7MessageStatus Status,
    DateTime ReceivedUtc,
    DateTime? ProcessedUtc,
    string? AckCode,
    string? ErrorDetail)
{
    public static Hl7MessageDto From(Hl7MessageLog m) => new(
        m.Id, m.Direction, m.MessageType, m.TriggerEvent, m.MessageControlId,
        m.Status, m.ReceivedUtc, m.ProcessedUtc, m.AckCode, m.ErrorDetail);
}

public sealed record Hl7ErrorDto(
    long Id,
    long Hl7MessageId,
    string ErrorType,
    string ErrorDetail,
    int RetryCount,
    DateTime? NextRetryUtc,
    bool Resolved)
{
    public static Hl7ErrorDto From(InterfaceErrorQueueItem e) => new(
        e.Id, e.Hl7MessageId, e.ErrorType, e.ErrorDetail, e.RetryCount, e.NextRetryUtc, e.Resolved);
}

public static class Hl7Endpoints
{
    public static void MapHl7Endpoints(this WebApplication app)
    {
        // The HTTP-facing HL7 surface is an operational/management interface; a real
        // interface engine authenticates at the gateway/MLLP transport. All routes here
        // require the hl7.manage permission.
        var group = app.MapGroup("/api/hl7").WithTags("HL7 Interface")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.Hl7Manage);

        // Accepts a raw HL7 v2.x message (text/plain) and returns the ACK/NAK as text.
        group.MapPost("/inbound", async (HttpRequest request, Hl7InboundProcessor processor, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var raw = await reader.ReadToEndAsync(ct);
            var outcome = await processor.ProcessAsync(raw, endpointId: null, isReplay: false, ct);
            return Results.Text(outcome.AckMessage, "text/plain", statusCode: outcome.Accepted ? 200 : 422);
        });

        group.MapGet("/messages", async (IRepository<Hl7MessageLog> repo, CancellationToken ct) =>
        {
            var messages = await repo.ListAsync(ct);
            return Results.Ok(messages.OrderByDescending(m => m.ReceivedUtc).Select(Hl7MessageDto.From));
        });

        group.MapGet("/messages/{id:long}", async (long id, IRepository<Hl7MessageLog> repo, CancellationToken ct) =>
        {
            var message = await repo.GetByIdAsync(id, ct);
            return message is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    message = Hl7MessageDto.From(message),
                    raw = message.RawMessage
                });
        });

        group.MapPost("/messages/{id:long}/replay", async (long id, Hl7InboundProcessor processor, CancellationToken ct) =>
        {
            var outcome = await processor.ReplayAsync(id, ct);
            if (outcome is null)
            {
                return Results.NotFound(new { error = $"Inbound message {id} not found." });
            }

            return Results.Ok(new { ackCode = outcome.AckCode, ack = outcome.AckMessage, logId = outcome.Log.Id });
        });

        group.MapGet("/errors", async (IRepository<InterfaceErrorQueueItem> repo, CancellationToken ct) =>
        {
            var errors = await repo.ListAsync(e => !e.Resolved, ct);
            return Results.Ok(errors.Select(Hl7ErrorDto.From));
        });

        // Queues an outbound ORU for a verified result (transport handled by the sender).
        group.MapPost("/outbound/results/{resultId:long}", async (long resultId, Hl7OutboundService service, CancellationToken ct) =>
            EndpointResults.Created(await service.QueueResultMessageAsync(resultId, ct),
                m => ($"/api/hl7/messages/{m.Id}", (object)Hl7MessageDto.From(m))));

        group.MapPost("/messages/{id:long}/send", async (long id, Hl7OutboundSender sender, CancellationToken ct) =>
            EndpointResults.From(await sender.SendOneAsync(id, ct), Hl7MessageDto.From));

        group.MapPost("/outbound/flush", async (Hl7OutboundSender sender, CancellationToken ct) =>
        {
            var sent = await sender.SendPendingAsync(ct: ct);
            return Results.Ok(new { sent });
        });
    }
}
