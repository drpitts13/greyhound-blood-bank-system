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
    }

    private static void MapSpecialRequirements(WebApplication app)
    {
        var group = app.MapGroup("/api/patients/{patientId:long}/special-requirements")
            .WithTags("SpecialRequirements")
            .RequireAuthenticatedUser();

        group.MapGet("/", async (long patientId, SpecialRequirementService service, CancellationToken ct) =>
            Results.Ok((await service.ListAsync(patientId, ct)).Select(SpecialRequirementDto.From)));

        group.MapPost("/", async (long patientId, AddSpecialRequirementRequest request, SpecialRequirementService service, CancellationToken ct) =>
            EndpointResults.Created(await service.AddAsync(patientId, request, ct),
                r => ($"/api/patients/{patientId}/special-requirements/{r.Id}", (object)SpecialRequirementDto.From(r))))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        app.MapPost("/api/special-requirements/{id:long}/deactivate", async (long id, ReasonBody request, SpecialRequirementService service, CancellationToken ct) =>
            EndpointResults.From(await service.DeactivateAsync(id, request.Reason, ct), SpecialRequirementDto.From))
            .RequirePermission(PermissionCodes.ImmunoOverride)
            .WithTags("SpecialRequirements");
    }

    private static void MapLookback(WebApplication app)
    {
        var group = app.MapGroup("/api/lookback").WithTags("Lookback").RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.LookbackManage);

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
            Results.Ok((await service.ListAsync(ct)).Select(ReactionInvestigationDto.From)));

        group.MapGet("/{id:long}", async (long id, ReactionInvestigationService service, CancellationToken ct) =>
        {
            var row = await service.GetAsync(id, ct);
            return row is null ? Results.NotFound() : Results.Ok(ReactionInvestigationDto.From(row));
        });

        group.MapPut("/{id:long}", async (long id, UpdateReactionInvestigationRequest request, ReactionInvestigationService service, CancellationToken ct) =>
            EndpointResults.From(await service.UpdateAsync(id, request, ct), ReactionInvestigationDto.From));

        group.MapPost("/{id:long}/cber-notified", async (long id, ReactionInvestigationService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordCberNotificationAsync(id, ct), ReactionInvestigationDto.From));

        group.MapPost("/{id:long}/written-report", async (long id, ReactionInvestigationService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordWrittenReportAsync(id, ct), ReactionInvestigationDto.From));
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
}

public sealed record ReasonBody(string Reason);

public sealed record DeviationStatusBody(DeviationStatus Status, string? CorrectiveAction = null);
