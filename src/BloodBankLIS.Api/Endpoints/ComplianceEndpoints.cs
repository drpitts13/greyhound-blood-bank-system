using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class ComplianceEndpoints
{
    public static void MapComplianceEndpoints(this WebApplication app)
    {
        MapSpecialRequirements(app);
        MapLookback(app);
        MapReactions(app);
        MapDeviations(app);
        MapDowntimeReconciliation(app);
    }

    private static void MapSpecialRequirements(WebApplication app)
    {
        var group = app.MapGroup("/api/patients/{patientId:long}/special-requirements")
            .WithTags("SpecialRequirements")
            .RequireAuthenticatedUser();

        group.MapGet("/", async (long patientId, SpecialRequirementService service, CancellationToken ct) =>
            Results.Ok(await service.ListDtosAsync(patientId, ct)));

        group.MapPost("/", async (long patientId, AddSpecialRequirementRequest request, SpecialRequirementService service, CancellationToken ct) =>
            EndpointResults.Created(await service.AddAsync(patientId, request, ct),
                r => ($"/api/patients/{patientId}/special-requirements/{r.Id}", (object)SpecialRequirementDto.From(r))))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        app.MapPost("/api/special-requirements/{id:long}/deactivate", async (long id, ReasonBody request, SpecialRequirementService service, CancellationToken ct) =>
            EndpointResults.From(await service.DeactivateAsync(id, request.Reason, ct), r => SpecialRequirementDto.From(r)))
            .RequirePermission(PermissionCodes.ImmunoOverride)
            .WithTags("SpecialRequirements");
    }

    private static void MapLookback(WebApplication app)
    {
        var group = app.MapGroup("/api/lookback").WithTags("Lookback").RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.LookbackManage);

        group.MapGet("/recipient", async (string? mrn, long? patientId, LookbackService service, CancellationToken ct) =>
            EndpointResults.From(await service.FindByRecipientAsync(mrn, patientId, ct), r => r));

        group.MapGet("/{din}", async (string din, LookbackService service, CancellationToken ct) =>
            EndpointResults.From(await service.FindByDinAsync(din, ct), r => r));

        group.MapPost("/{din}/recall", async (string din, ReasonBody request, LookbackService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecallByDinAsync(din, request.Reason, ct), r => r));

        group.MapPost("/notifications/{id:long}", async (long id, RecordLookbackAttemptRequest request, LookbackService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordAttemptAsync(id, request, ct), LookbackNotificationDto.From));
    }

    private static void MapReactions(WebApplication app)
    {
        var group = app.MapGroup("/api/reaction-investigations").WithTags("Reactions")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.ReactionInvestigate);

        group.MapGet("/", async (ReactionInvestigationService service, CancellationToken ct) =>
            Results.Ok(await service.ListDtosAsync(ct)));

        group.MapGet("/{id:long}", async (long id, ReactionInvestigationService service, CancellationToken ct) =>
        {
            var row = await service.GetDtoAsync(id, ct);
            return row is null ? Results.NotFound() : Results.Ok(row);
        });

        group.MapPut("/{id:long}", async (long id, UpdateReactionInvestigationRequest request, ReactionInvestigationService service, CancellationToken ct) =>
            EndpointResults.From(await service.UpdateAsync(id, request, ct), r => ReactionInvestigationDto.From(r)));

        group.MapPost("/{id:long}/cber-notified", async (long id, ReactionInvestigationService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordCberNotificationAsync(id, ct), r => ReactionInvestigationDto.From(r)));

        group.MapPost("/{id:long}/written-report", async (long id, ReactionInvestigationService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordWrittenReportAsync(id, ct), r => ReactionInvestigationDto.From(r)));
    }

    private static void MapDeviations(WebApplication app)
    {
        var group = app.MapGroup("/api/deviations").WithTags("Deviations")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.DeviationManage);

        group.MapGet("/", async (DeviationService service, CancellationToken ct) =>
            Results.Ok((await service.ListAsync(ct)).Select(DeviationDto.From)));

        group.MapPost("/", async (CreateDeviationRequest request, DeviationService service, CancellationToken ct) =>
            EndpointResults.Created(await service.CreateAsync(request, ct),
                d => ($"/api/deviations/{d.Id}", (object)DeviationDto.From(d))));

        group.MapPost("/{id:long}/status", async (long id, DeviationStatusBody request, DeviationService service, CancellationToken ct) =>
            EndpointResults.From(await service.UpdateStatusAsync(id, request.Status, request.CorrectiveAction, ct), DeviationDto.From));
    }

    private static void MapDowntimeReconciliation(WebApplication app)
    {
        app.MapGet("/api/compliance/downtime-reconciliation", async (
                DowntimeReconciliationService service, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await service.GetSnapshotAsync(ct), s => s))
            .WithTags("Compliance")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.AuditRead);
    }
}

public sealed record ReasonBody(string Reason);

public sealed record DeviationStatusBody(DeviationStatus Status, string? CorrectiveAction = null);
