using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public sealed record RejectSpecimenRequest(string Reason);

public static class SpecimenEndpoints
{
    public static void MapSpecimenEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/specimens").WithTags("Specimens").RequireAuthenticatedUser();

        group.MapGet("/{id:long}", async (long id, SpecimenService service, CancellationToken ct) =>
        {
            var specimen = await service.GetAsync(id, ct);
            return specimen is null ? Results.NotFound() : Results.Ok(specimen);
        });

        group.MapPost("/", async (AccessionSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.AccessionAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return Results.Created($"/api/specimens/{result.Value.Id}", dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPut("/{id:long}", async (long id, UpdateSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenEdit);

        group.MapPost("/{id:long}/reject", async (long id, RejectSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.RejectAsync(id, request.Reason, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenReject);

        app.MapGet("/api/patients/{patientId:long}/specimens", async (long patientId, SpecimenService service, CancellationToken ct) =>
        {
            var specimens = await service.GetByPatientAsync(patientId, ct);
            return Results.Ok(specimens);
        }).WithTags("Specimens");
    }
}
