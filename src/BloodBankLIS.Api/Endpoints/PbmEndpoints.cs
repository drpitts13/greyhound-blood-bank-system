using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>Patient-blood-management ordering criteria (OCD-038): check, review, and the admin catalog.</summary>
public static class PbmEndpoints
{
    public static void MapPbmEndpoints(this WebApplication app)
    {
        app.MapPost("/api/patients/{patientId:long}/pbm-check", async (
                long patientId, PbmCheckRequest request, TransfusionIndicationService service, IClock clock, CancellationToken ct) =>
                Results.Ok(await service.EvaluateAsync(patientId, request.ProductTypeIds ?? [], clock.UtcNow, request.Indications, ct)))
            .WithTags("Patient blood management")
            .RequireAuthenticatedUser();

        app.MapGet("/api/pbm/review", async (DateTime? fromUtc, DateTime? toUtc, TransfusionIndicationService service, CancellationToken ct) =>
                Results.Ok(await service.ListOutsideCriteriaAsync(fromUtc, toUtc, ct)))
            .WithTags("Patient blood management")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.ReportRun);

        var admin = app.MapGroup("/api/admin/transfusion-criteria")
            .WithTags("Admin: Transfusion Criteria")
            .RequireAuthenticatedUser();

        admin.MapGet("", async (TransfusionIndicationAdminService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        admin.MapPost("", async (SaveTransfusionIndicationCriterionRequest req, TransfusionIndicationAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        admin.MapPut("/{id:long}", async (long id, SaveTransfusionIndicationCriterionRequest req, TransfusionIndicationAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        admin.MapPost("/{id:long}/activate", async (long id, TransfusionIndicationAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, true, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        admin.MapPost("/{id:long}/deactivate", async (long id, TransfusionIndicationAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, false, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }
}
