using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Api.Endpoints;

public sealed record DeactivateAntibodyRequest(string Reason);

public static class ImmunohematologyEndpoints
{
    public static void MapImmunohematologyEndpoints(this WebApplication app)
    {
        var patients = app.MapGroup("/api/patients").WithTags("Immunohematology").RequireAuthenticatedUser();

        patients.MapGet("/{patientId:long}/blood-type", async (long patientId, ImmunohematologyService service, CancellationToken ct) =>
        {
            var current = await service.GetCurrentBloodTypeAsync(patientId, ct);
            return current is null ? Results.NotFound() : Results.Ok(BloodTypeDto.From(current));
        });

        patients.MapGet("/{patientId:long}/blood-type/history", async (long patientId, ImmunohematologyService service, CancellationToken ct) =>
        {
            var history = await service.GetBloodTypeHistoryAsync(patientId, ct);
            return Results.Ok(history.Select(BloodTypeDto.From));
        });

        patients.MapPost("/{patientId:long}/blood-type", async (long patientId, RecordBloodTypeRequest request, ImmunohematologyService service, CancellationToken ct) =>
            EndpointResults.Created(await service.RecordBloodTypeManualAsync(patientId, request.Abo, request.RhD, request.Reason, request.AboSubgroup,
                    request.Documentation, request.DocumentationReference, ct),
                h => ($"/api/patients/{patientId}/blood-type", (object)BloodTypeDto.From(h))))
            .RequirePermission(PermissionCodes.ImmunoOverride);

        // RhIG evaluations (OCD-069): facts on file, the decision and dose recorded by the reviewer.
        app.MapGet("/api/rhig/given", async (string? lot, RhigEvaluationService service, CancellationToken ct) =>
            Results.Ok(await service.FindByLotAsync(lot, ct)))
            .WithTags("Immunohematology")
            .RequireAuthenticatedUser();

        // The RhIG lot register (Cycle 721): read by anyone signed in; receiving and retiring are issue work.
        app.MapGet("/api/rhig/lots", async (bool? includeRetired, RhigLotService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(includeRetired ?? false, ct))).RequireAuthenticatedUser();
        app.MapPost("/api/rhig/lots", async (ReceiveRhigLotRequest request, RhigLotService service, CancellationToken ct) =>
            {
                var r = await service.ReceiveAsync(request, ct);
                return r.Succeeded ? Results.Ok(r.Value) : Results.BadRequest(new { error = r.Error });
            })
            .RequireAuthenticatedUser().RequirePermission(PermissionCodes.IssueCreate);
        app.MapPost("/api/rhig/lots/{id:long}/retire", async (long id, RetireRhigLotRequest request, RhigLotService service, CancellationToken ct) =>
            {
                var r = await service.RetireAsync(id, request, ct);
                return r.Succeeded ? Results.Ok(r.Value) : Results.BadRequest(new { error = r.Error });
            })
            .RequireAuthenticatedUser().RequirePermission(PermissionCodes.IssueCreate);

        app.MapGet("/api/rhig/open", async (RhigEvaluationService service, CancellationToken ct) =>
            Results.Ok(await service.ListOpenAsync(ct)))
            .WithTags("Immunohematology")
            .RequireAuthenticatedUser();

        patients.MapGet("/{patientId:long}/rhig", async (long patientId, RhigEvaluationService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(patientId, ct)));

        patients.MapPost("/{patientId:long}/rhig", async (long patientId, SaveRhigEvaluationRequest request, RhigEvaluationService service, CancellationToken ct) =>
            EndpointResults.From(await service.SaveAsync(patientId, request, ct), d => d))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        patients.MapPost("/{patientId:long}/rhig/{id:long}/given", async (long patientId, long id, RecordRhigGivenRequest request, RhigEvaluationService service, CancellationToken ct) =>
            EndpointResults.From(await service.RecordGivenAsync(patientId, id, request, ct), d => d))
            .RequirePermission(PermissionCodes.IssueCreate);

        patients.MapPost("/{patientId:long}/rhig/{id:long}/complete", async (long patientId, long id, RhigEvaluationService service, CancellationToken ct) =>
            EndpointResults.From(await service.CompleteAsync(patientId, id, ct), d => d))
            .RequirePermission(PermissionCodes.ResultVerify);

        patients.MapGet("/{patientId:long}/antibodies", async (long patientId, ImmunohematologyService service, CancellationToken ct) =>
        {
            var antibodies = await service.ListAntibodyDtosAsync(patientId, activeOnly: true, ct);
            return Results.Ok(antibodies);
        });

        patients.MapGet("/{patientId:long}/antibodies/history", async (long patientId, ImmunohematologyService service, CancellationToken ct) =>
        {
            var antibodies = await service.ListAntibodyDtosAsync(patientId, activeOnly: false, ct);
            return Results.Ok(antibodies);
        });

        patients.MapGet("/{patientId:long}/antigen-profiles", async (long patientId, ImmunohematologyService service, BloodBankDbContext context, CancellationToken ct) =>
        {
            var profiles = await service.GetAntigenProfilesAsync(patientId, ct);
            var defs = await context.BloodAttributeDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Id, ct);
            return Results.Ok(profiles
                .Where(p => defs.ContainsKey(p.BloodAttributeDefinitionId))
                .Select(p =>
                {
                    var d = defs[p.BloodAttributeDefinitionId];
                    return AntigenProfileDto.From(p, d.Code, d.Name);
                }));
        });

        patients.MapPost("/{patientId:long}/antigen-profiles", async (long patientId, SaveAntigenProfileRequest request, ImmunohematologyService service, BloodBankDbContext context, CancellationToken ct) =>
        {
            var result = await service.SaveAntigenProfileAsync(patientId, request, ct);
            if (!result.Succeeded || result.Value is null)
            {
                return EndpointResults.From(result, p => p);
            }

            var def = await context.BloodAttributeDefinitions.AsNoTracking()
                .FirstAsync(d => d.Id == result.Value.BloodAttributeDefinitionId, ct);
            return EndpointResults.Created(result, p => ($"/api/patients/{patientId}/antigen-profiles/{p.Id}", (object)AntigenProfileDto.From(p, def.Code, def.Name)));
        }).RequirePermission(PermissionCodes.ImmunoRecord);

        patients.MapPost("/{patientId:long}/antibodies", async (long patientId, AddAntibodyRequest request, ImmunohematologyService service, CancellationToken ct) =>
            EndpointResults.Created(await service.AddAntibodyAsync(patientId, request.BloodAttributeDefinitionId, request.AntibodySpecificity, request.Status, request.Comment, request.ReportingFacility, request.IdentifiedOn, request.ReportKind, request.ReportDocumentation, ct),
                a => ($"/api/patients/{patientId}/antibodies/{a.Id}", (object)AntibodyDto.From(a))))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        // OCD-069: a weak D or partial D recorded for the patient (treated as Rh negative for RhIG).
        app.MapPost("/api/patients/{patientId:long}/rhd-variant", async (long patientId, SetRhDVariantRequest request, ImmunohematologyService service, CancellationToken ct) =>
            EndpointResults.From(await service.SetRhDVariantAsync(patientId, request.Variant, request.Reason, ct), p => BloodBankLIS.Application.Patients.PatientDto.From(p)))
            .RequirePermission(PermissionCodes.ImmunoRecord)
            .WithTags("Immunohematology");

        // OCD-073: outside reports waiting for this laboratory to confirm them.
        app.MapGet("/api/antibodies/outside-to-confirm", async (ImmunohematologyService service, CancellationToken ct) =>
            Results.Ok(await service.ListOutsideToConfirmAsync(ct)))
            .RequirePermission(PermissionCodes.ImmunoRecord)
            .WithTags("Immunohematology");

        // OCD-073: this laboratory confirms an antibody reported by another facility.
        app.MapPost("/api/antibodies/{id:long}/confirm-outside", async (long id, ConfirmOutsideAntibodyRequest request, ImmunohematologyService service, CancellationToken ct) =>
            EndpointResults.From(await service.ConfirmOutsideAntibodyAsync(id, request.Note, ct), a => AntibodyDto.From(a)))
            .RequirePermission(PermissionCodes.ImmunoRecord)
            .WithTags("Immunohematology");

        app.MapPost("/api/antibodies/{id:long}/deactivate", async (long id, DeactivateAntibodyRequest request, ImmunohematologyService service, CancellationToken ct) =>
            EndpointResults.From(await service.DeactivateAntibodyAsync(id, request.Reason, ct), a => AntibodyDto.From(a)))
            .RequirePermission(PermissionCodes.ImmunoOverride)
            .WithTags("Immunohematology");

        patients.MapGet("/{patientId:long}/dat-elution", async (long patientId, DatElutionWorkupService service, CancellationToken ct) =>
            Results.Ok(await service.ListForPatientAsync(patientId, ct)));

        patients.MapPost("/{patientId:long}/dat-elution", async (
                long patientId,
                CreateDatElutionWorkupRequest request,
                DatElutionWorkupService service,
                CancellationToken ct) =>
            EndpointResults.Created(
                await service.CreateAsync(patientId, request, ct),
                w => ($"/api/dat-elution/{w.Id}", (object)DatElutionWorkupDto.From(w))))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        var datElution = app.MapGroup("/api/dat-elution").WithTags("DatElution").RequireAuthenticatedUser();

        datElution.MapGet("/{id:long}", async (long id, DatElutionWorkupService service, CancellationToken ct) =>
        {
            var row = await service.GetAsync(id, ct);
            return row is null ? Results.NotFound() : Results.Ok(row);
        });

        datElution.MapPut("/{id:long}", async (long id, UpdateDatElutionWorkupRequest request, DatElutionWorkupService service, CancellationToken ct) =>
            EndpointResults.From(await service.UpdateAsync(id, request, ct), w => DatElutionWorkupDto.From(w)))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        datElution.MapPost("/{id:long}/complete", async (long id, DatElutionWorkupService service, CancellationToken ct) =>
            EndpointResults.From(await service.CompleteAsync(id, ct), w => DatElutionWorkupDto.From(w)))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        datElution.MapPost("/{id:long}/void", async (long id, VoidDatElutionWorkupRequest request, DatElutionWorkupService service, CancellationToken ct) =>
            EndpointResults.From(await service.VoidAsync(id, request, ct), w => DatElutionWorkupDto.From(w)))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        // OCD-042: structured eluate findings; nothing posts until a second person verifies the completed workup.
        datElution.MapPost("/{id:long}/eluate-findings", async (long id, RecordEluateFindingsRequest request, DatElutionWorkupService service, CancellationToken ct) =>
            {
                var result = await service.RecordEluateFindingsAsync(id, request, ct);
                return result.Succeeded
                    ? Results.Ok(await service.GetAsync(id, ct))
                    : EndpointResults.From(result, w => DatElutionWorkupDto.From(w));
            })
            .RequirePermission(PermissionCodes.ImmunoRecord);

        datElution.MapPost("/{id:long}/verify", async (long id, DatElutionWorkupService service, CancellationToken ct) =>
            {
                var result = await service.VerifyAsync(id, ct);
                return result.Succeeded
                    ? Results.Ok(await service.GetAsync(id, ct))
                    : EndpointResults.From(result, w => DatElutionWorkupDto.From(w));
            })
            .RequirePermission(PermissionCodes.ImmunoOverride);
    }
}
