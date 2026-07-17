using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public sealed record ReleaseAllocationRequest(string Reason);

public static class CompatibilityEndpoints
{
    public static void MapCompatibilityEndpoints(this WebApplication app)
    {
        app.MapPost("/api/compatibility/evaluate", async (EvaluateCompatibilityRequest request, CompatibilityService service, CancellationToken ct) =>
        {
            var result = await service.EvaluateCompatibilityAsync(request, ct);
            if (!result.Succeeded || result.Value is null)
            {
                return result.Evaluation is not null
                    ? EndpointResults.FromEvaluation(result, e => e)
                    : Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(new
            {
                allowed = result.Value.IsAllowed,
                requiresOverride = result.Value.RequiresOverride,
                hardStops = result.Value.HardStops.Select(r => new { r.Code, r.Message }),
                warnings = result.Value.Warnings.Select(r => new { r.Code, r.Message })
            });
        }).RequirePermission(PermissionCodes.CompatibilityCrossmatch).WithTags("Compatibility");

        var crossmatches = app.MapGroup("/api/crossmatches").WithTags("Compatibility").RequireAuthenticatedUser();

        crossmatches.MapPost("/", async (RecordCrossmatchRequest request, CompatibilityService service, CancellationToken ct) =>
            EndpointResults.CreatedEvaluation(await service.RecordCrossmatchAsync(request, ct),
                x => ($"/api/crossmatches/{x.Id}", (object)CrossmatchDto.From(x))))
            .RequirePermission(PermissionCodes.CompatibilityCrossmatch);

        var allocations = app.MapGroup("/api/allocations").WithTags("Compatibility").RequireAuthenticatedUser();

        allocations.MapPost("/", async (AllocateUnitRequest request, CompatibilityService service, CancellationToken ct) =>
            EndpointResults.CreatedEvaluation(await service.AllocateUnitAsync(request, ct),
                a => ($"/api/allocations/{a.Id}", (object)AllocationDto.From(a))))
            .RequirePermission(PermissionCodes.CompatibilityAllocate);

        allocations.MapPost("/{id:long}/release", async (long id, ReleaseAllocationRequest request, CompatibilityService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.ReleaseAllocationAsync(id, request.Reason, ct), a => AllocationDto.From(a)))
            .RequirePermission(PermissionCodes.CompatibilityAllocate);
    }
}
