using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Common;
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
            return dto is null
                ? Results.NotFound()
                : EndpointResults.From(
                    OperationResult<SpecimenDto>.Ok(dto, result.Warnings),
                    payload => payload!);
        }).RequirePermission(PermissionCodes.SpecimenEdit);

        group.MapGet("/pending", async (SpecimenService service, CancellationToken ct) =>
            Results.Ok(await service.ListPendingReceiptAsync(ct)))
            .RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/{id:long}/relabel", async (long id, RelabelSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.RelabelAsync(id, request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenEdit);

        group.MapPost("/{id:long}/receive", async (long id, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.ReceiveAsync(id, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/{id:long}/accept", async (long id, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.AcceptAsync(id, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/{id:long}/reject", async (long id, RejectSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.RejectAsync(id, request.Reason, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null
                ? Results.NotFound()
                : EndpointResults.From(
                    OperationResult<SpecimenDto>.Ok(dto, result.Warnings),
                    payload => payload!);
        }).RequirePermission(PermissionCodes.SpecimenReject);

        app.MapGet("/api/patients/{patientId:long}/specimens", async (long patientId, SpecimenService service, CancellationToken ct) =>
        {
            var specimens = await service.GetByPatientAsync(patientId, ct);
            return Results.Ok(specimens);
        }).WithTags("Specimens");
    }
}
