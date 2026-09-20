using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Astm.Framing;
using BloodBankLIS.Astm.Messaging;
using BloodBankLIS.Astm.Simulator;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class InstrumentEndpoints
{
    public static void MapInstrumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/instruments").WithTags("Instrument Interfaces")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.InstrumentManage);

        group.MapGet("/status", async (
            IInstrumentInterfaceRepository interfaces,
            IInstrumentRuntimeStatus status,
            CancellationToken ct) =>
        {
            var items = await interfaces.ListWithChildrenAsync(ct);
            return Results.Ok(items.Select(i =>
            {
                var snap = status.Get(i.Id);
                var conn = i.Connection;
                return new InstrumentStatusDto(
                    i.Id, i.Name, i.IsEnabled, conn?.IsServer ?? true, conn?.IpAddress, conn?.Port ?? 0,
                    i.IsEnabled ? snap.State : "Disabled", snap.Detail, snap.LastEventUtc);
            }).ToList());
        });

        group.MapGet("/logs", async (long? interfaceId, IRepository<InstrumentInterfaceLog> logs, CancellationToken ct) =>
        {
            var rows = interfaceId is long id
                ? await logs.ListAsync(l => l.InterfaceId == id, ct)
                : await logs.ListAsync(ct);
            return Results.Ok(rows
                .OrderByDescending(l => l.OccurredUtc)
                .Take(500)
                .Select(l => new InstrumentLogDto(l.Id, l.InterfaceId, l.OccurredUtc, l.Level, l.EventType, l.Message))
                .ToList());
        });

        group.MapGet("/messages", async (long? interfaceId, IRepository<InstrumentMessageLog> messages, CancellationToken ct) =>
        {
            var rows = interfaceId is long id
                ? await messages.ListAsync(m => m.InterfaceId == id, ct)
                : await messages.ListAsync(ct);
            return Results.Ok(rows
                .OrderByDescending(m => m.ReceivedUtc)
                .Take(200)
                .Select(ToSummary)
                .ToList());
        });

        group.MapGet("/messages/{id:long}", async (long id, IRepository<InstrumentMessageLog> messages, CancellationToken ct) =>
        {
            var message = await messages.GetByIdAsync(id, ct);
            return message is null
                ? Results.NotFound()
                : Results.Ok(new InstrumentMessageDetailDto(
                    ToSummary(message), message.RawMessage, message.ParsedJson, message.MappedJson, message.RuleTraceJson));
        });

        group.MapPost("/messages/{id:long}/replay", async (long id, AstmInboundProcessor processor, CancellationToken ct) =>
        {
            var outcome = await processor.ReplayAsync(id, ct);
            return outcome is null
                ? Results.NotFound(new { error = $"Message {id} not found." })
                : Results.Ok(new { accepted = outcome.Accepted, notes = outcome.Notes, logId = outcome.Log.Id });
        });

        group.MapPost("/inbound/{interfaceId:long}", async (
            long interfaceId,
            HttpRequest request,
            AstmInboundProcessor processor,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var raw = await reader.ReadToEndAsync(ct);
            var outcome = await processor.ProcessAsync(raw, interfaceId, isReplay: false, ct);
            return Results.Json(new { accepted = outcome.Accepted, notes = outcome.Notes, logId = outcome.Log.Id, reply = outcome.ReplyMessage },
                statusCode: outcome.Accepted ? 200 : 422);
        });

        group.MapGet("/simulator/assays", () => Results.Ok(EchoResultSimulator.Assays));

        group.MapPost("/simulator/preview", (EchoSimulatorPreviewRequest req) =>
            Results.Ok(ToPreview(EchoResultSimulator.Generate(ToSim(req)))));

        group.MapPost("/simulator/send", async (
            EchoSimulatorSendRequest req,
            IInstrumentInterfaceRepository interfaces,
            AstmInboundProcessor processor,
            CancellationToken ct) =>
        {
            var generated = EchoResultSimulator.Generate(ToSim(req.Preview));
            var iface = await interfaces.GetWithChildrenAsync(req.InterfaceId, ct);
            if (iface?.Connection is null)
            {
                return Results.NotFound(new { error = "Instrument interface not found." });
            }

            if (iface.Connection.IsServer)
            {
                try
                {
                    var sent = await AstmSession.SendToAsync("127.0.0.1", iface.Connection.Port, generated.RawMessage, null, ct);
                    return Results.Ok(new EchoSimulatorSendResultDto(sent.Completed, sent.Error, null, generated.RawMessage));
                }
                catch (Exception ex)
                {
                    var outcome = await processor.ProcessAsync(generated.RawMessage, req.InterfaceId, isReplay: false, ct);
                    return Results.Ok(new EchoSimulatorSendResultDto(
                        outcome.Accepted, ex.Message + " Processed locally instead.", outcome.Log.Id, generated.RawMessage));
                }
            }

            var local = await processor.ProcessAsync(generated.RawMessage, req.InterfaceId, isReplay: false, ct);
            return Results.Ok(new EchoSimulatorSendResultDto(local.Accepted, local.Log.ErrorDetail, local.Log.Id, generated.RawMessage));
        });
    }

    private static InstrumentMessageSummaryDto ToSummary(InstrumentMessageLog m) =>
        new(m.Id, m.InterfaceId, m.Direction, m.MessageKind, m.Status, m.ReceivedUtc, m.ProcessedUtc, m.SpecimenId, m.PatientId, m.ErrorDetail);

    private static EchoSimulatorRequest ToSim(EchoSimulatorPreviewRequest req) =>
        new(req.Assay, req.SpecimenId, req.PatientId, req.LastName, req.FirstName, req.DateOfBirth, req.Sex, req.Seed);

    private static EchoSimulatorPreviewDto ToPreview(EchoSimulatorResult result) =>
        new(result.Assay, result.RawMessage, result.Records);
}
