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
            EndpointResults.Created(await service.RecordBloodTypeManualAsync(patientId, request.Abo, request.RhD, request.Reason, ct),
                h => ($"/api/patients/{patientId}/blood-type", (object)BloodTypeDto.From(h))))
            .RequirePermission(PermissionCodes.ImmunoOverride);

        patients.MapGet("/{patientId:long}/antibodies", async (long patientId, ImmunohematologyService service, CancellationToken ct) =>
        {
            var antibodies = await service.GetActiveAntibodiesAsync(patientId, ct);
            return Results.Ok(antibodies.Select(AntibodyDto.From));
        });

        patients.MapGet("/{patientId:long}/antibodies/history", async (long patientId, ImmunohematologyService service, CancellationToken ct) =>
        {
            var antibodies = await service.GetAntibodyHistoryAsync(patientId, ct);
            return Results.Ok(antibodies.Select(AntibodyDto.From));
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
            EndpointResults.Created(await service.AddAntibodyAsync(patientId, request.BloodAttributeDefinitionId, request.AntibodySpecificity, request.Status, request.Comment, ct),
                a => ($"/api/patients/{patientId}/antibodies/{a.Id}", (object)AntibodyDto.From(a))))
            .RequirePermission(PermissionCodes.ImmunoRecord);

        app.MapPost("/api/antibodies/{id:long}/deactivate", async (long id, DeactivateAntibodyRequest request, ImmunohematologyService service, CancellationToken ct) =>
            EndpointResults.From(await service.DeactivateAntibodyAsync(id, request.Reason, ct), a => AntibodyDto.From(a)))
            .RequirePermission(PermissionCodes.ImmunoOverride)
            .WithTags("Immunohematology");
    }
}
