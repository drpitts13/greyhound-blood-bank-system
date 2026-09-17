using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Parsing;

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
    string? ErrorDetail,
    string? PatientMrn = null,
    string? PatientDisplayName = null,
    string? PlacerOrderNumber = null,
    string? TestCode = null,
    string? UnitNumber = null,
    long? PatientId = null)
{
    public static Hl7MessageDto From(Hl7MessageLog m, long? patientId = null)
    {
        Hl7MessageIdentity.TryRead(m.RawMessage, out var identity);
        return new(
            m.Id, m.Direction, m.MessageType, m.TriggerEvent, m.MessageControlId,
            m.Status, m.ReceivedUtc, m.ProcessedUtc, m.AckCode, m.ErrorDetail,
            identity.MedicalRecordNumber, identity.DisplayName,
            identity.PlacerOrderNumber, identity.TestCode, identity.UnitNumber,
            patientId);
    }
}

public sealed record Hl7ErrorDto(
    long Id,
    long Hl7MessageId,
    string ErrorType,
    string ErrorDetail,
    int RetryCount,
    DateTime? NextRetryUtc,
    bool Resolved,
    string? MessageControlId = null,
    string? MessageType = null,
    string? TriggerEvent = null,
    string? AckCode = null,
    string? PatientMrn = null,
    string? PatientDisplayName = null,
    Hl7Direction? Direction = null,
    string? PlacerOrderNumber = null,
    string? TestCode = null,
    string? UnitNumber = null,
    long? PatientId = null)
{
    public static Hl7ErrorDto From(
        InterfaceErrorQueueItem e,
        Hl7MessageLog? message = null,
        long? patientId = null)
    {
        Hl7MessageIdentity.TryRead(message?.RawMessage, out var identity);
        return new(
            e.Id, e.Hl7MessageId, e.ErrorType, e.ErrorDetail, e.RetryCount, e.NextRetryUtc, e.Resolved,
            message?.MessageControlId, message?.MessageType, message?.TriggerEvent, message?.AckCode,
            identity.MedicalRecordNumber, identity.DisplayName,
            message?.Direction, identity.PlacerOrderNumber, identity.TestCode, identity.UnitNumber,
            patientId);
    }
}

public static class Hl7Endpoints
{
    public static void MapHl7Endpoints(this WebApplication app)
    {
        // HTTP inbound is an operational/management surface: session + hl7.manage.
        // MLLP and file-drop inbound trust the transport (OCD-034 / gap 5). Do not
        // invent a shared interface secret on those adapters.
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

        group.MapGet("/messages", async (
            IRepository<Hl7MessageLog> repo,
            IRepository<Patient> patients,
            CancellationToken ct) =>
        {
            var messages = await repo.ListAsync(ct);
            var dtos = messages
                .OrderByDescending(m => m.ReceivedUtc)
                .Select(m => Hl7MessageDto.From(m))
                .ToList();
            return Results.Ok(await AttachPatientIdsAsync(dtos, patients, ct));
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

        group.MapGet("/errors", async (
            IRepository<InterfaceErrorQueueItem> repo,
            IRepository<Hl7MessageLog> logs,
            IRepository<Patient> patients,
            CancellationToken ct) =>
        {
            var errors = await repo.ListAsync(e => !e.Resolved, ct);
            var messageIds = errors.Select(e => e.Hl7MessageId).Distinct().ToList();
            var messages = messageIds.Count == 0
                ? []
                : await logs.ListAsync(m => messageIds.Contains(m.Id), ct);
            var byId = messages.ToDictionary(m => m.Id);
            var dtos = errors
                .OrderByDescending(e => e.Id)
                .Select(e => Hl7ErrorDto.From(e, byId.GetValueOrDefault(e.Hl7MessageId)))
                .ToList();
            return Results.Ok(await AttachPatientIdsAsync(dtos, patients, ct));
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

        group.MapPost("/file-drop/poll", async (Hl7FileDropInboundPoller poller, CancellationToken ct) =>
        {
            var processed = await poller.PollAsync(ct: ct);
            return Results.Ok(new { processed });
        });
    }

    private static async Task<IReadOnlyList<T>> AttachPatientIdsAsync<T>(
        IReadOnlyList<T> rows,
        IRepository<Patient> patients,
        CancellationToken ct,
        Func<T, string?>? getMrn = null,
        Func<T, long, T>? withPatientId = null)
    {
        getMrn ??= row => row switch
        {
            Hl7MessageDto message => message.PatientMrn,
            Hl7ErrorDto error => error.PatientMrn,
            _ => null
        };
        withPatientId ??= (row, patientId) => row switch
        {
            Hl7MessageDto message => (T)(object)(message with { PatientId = patientId }),
            Hl7ErrorDto error => (T)(object)(error with { PatientId = patientId }),
            _ => row
        };

        var mrns = rows
            .Select(getMrn)
            .Where(mrn => !string.IsNullOrWhiteSpace(mrn))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (mrns.Count == 0)
        {
            return rows.ToList();
        }

        var matches = await patients.ListAsync(p => mrns.Contains(p.MedicalRecordNumber), ct);
        var byMrn = matches
            .GroupBy(p => p.MedicalRecordNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Id).First().Id, StringComparer.OrdinalIgnoreCase);

        return rows
            .Select(row =>
                getMrn(row) is string mrn && byMrn.TryGetValue(mrn, out var patientId)
                    ? withPatientId(row, patientId)
                    : row)
            .ToList();
    }
}
