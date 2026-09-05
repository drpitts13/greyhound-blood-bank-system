using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class AntibodyIdentificationEndpoints
{
    public static void MapAntibodyIdentificationEndpoints(this WebApplication app)
    {
        var lots = app.MapGroup("/api/antibody-id/lots").WithTags("AntibodyIdentification").RequireAuthenticatedUser();
        lots.MapGet("", async (AntibodyIdentificationService service, bool includeExpired, CancellationToken ct) =>
            Results.Ok(await service.ListLotsAsync(includeExpired, ct)));

        var patients = app.MapGroup("/api/patients/{patientId:long}/antibody-id")
            .WithTags("AntibodyIdentification")
            .RequireAuthenticatedUser();

        patients.MapGet("", async (long patientId, AntibodyIdentificationService service, CancellationToken ct) =>
            Results.Ok(await service.ListWorkupsAsync(patientId, ct)));

        patients.MapPost("", async (long patientId, CreateAntibodyIdWorkupRequest request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.CreatedEvaluation(
                await service.CreateWorkupAsync(patientId, request, ct),
                w => ($"/api/antibody-id/{w.Id}", (object)w)))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        var workups = app.MapGroup("/api/antibody-id").WithTags("AntibodyIdentification").RequireAuthenticatedUser();

        workups.MapGet("", async (AntibodyIdentificationService service, CancellationToken ct) =>
            Results.Ok(await service.ListOpenWorkupsAsync(ct)));

        workups.MapGet("/{id:long}", async (long id, AntibodyIdentificationService service, CancellationToken ct) =>
        {
            var workup = await service.GetWorkupAsync(id, ct);
            return workup is null ? Results.NotFound() : Results.Ok(workup);
        });

        workups.MapPost("/{id:long}/specimen", async (long id, LinkAntibodyIdSpecimenRequest request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.LinkSpecimenAsync(id, request, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        workups.MapPost("/{id:long}/lots", async (long id, AttachAntibodyIdLotsRequest request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.AttachLotsAsync(id, request, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        workups.MapPost("/{id:long}/reactions", async (long id, List<RecordAntibodyIdReactionRequest> request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordReactionsAsync(id, request, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        workups.MapPost("/{id:long}/dat", async (long id, RecordAntibodyIdDatRequest request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordDatAsync(id, request, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        workups.MapPost("/{id:long}/comment", async (long id, AntibodyIdCommentRequest request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordCommentAsync(id, request.Comment, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        workups.MapPost("/{id:long}/assist", async (long id, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.RunAssistAsync(id, ct), a => a))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        workups.MapPost("/{id:long}/interpretation", async (long id, RecordAntibodyIdInterpretationRequest request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.RecordInterpretationAsync(id, request, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        workups.MapPost("/{id:long}/review", async (long id, ReviewAntibodyIdWorkupRequest request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.ReviewAsync(id, request, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoOverride);

        workups.MapPost("/{id:long}/complete", async (long id, CompleteAntibodyIdWorkupRequest? request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.CompleteAsync(id, request, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        workups.MapPost("/{id:long}/void", async (long id, VoidAntibodyIdWorkupRequest request, AntibodyIdentificationService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.VoidAsync(id, request, ct), w => w))
            .RequirePermission(PermissionCodes.ImmunoRecord);
    }
}
