using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
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

        allocations.MapPost("/", async (
                AllocateUnitRequest request,
                CompatibilityService service,
                CrossmatchAttachmentService orders,
                IRepository<BloodUnit> units,
                IRepository<Allocation> allocationRepo,
                IUnitOfWork unitOfWork,
                CancellationToken ct) =>
            {
                var result = await service.AllocateUnitAsync(request, ct);
                // The reservation belongs to the patient's open order for the product, as on the chart's Allocate: end-user
                // testing pass 27, a unit reserved here was not linked to its order, so cancelling the order did not see it.
                if (result.Succeeded && result.Value is { OrderId: null } allocation
                    && await units.GetByIdAsync(allocation.BloodProductId, ct) is { } unit
                    && await orders.ResolveProductOrderAsync(allocation.PatientId, unit.ProductTypeId, null, ct) is { } order)
                {
                    allocation.OrderId = order.Id;
                    allocationRepo.Update(allocation);
                    await unitOfWork.SaveChangesAsync(ct);
                }

                return EndpointResults.CreatedEvaluation(result, a => ($"/api/allocations/{a.Id}", (object)AllocationDto.From(a)));
            })
            .RequirePermission(PermissionCodes.CompatibilityAllocate);

        // The ward asks for a reserved unit to be issued; the service allows transfusion.document or issue.create.
        allocations.MapPost("/{id:long}/issue-request", async (long id, IssueRequestRequest request, IssueRequestService service, CancellationToken ct) =>
            EndpointResults.From(await service.RequestAsync(id, request, ct), d => d));

        allocations.MapPost("/{id:long}/issue-request/cancel", async (long id, CancelIssueRequestRequest request, IssueRequestService service, CancellationToken ct) =>
            EndpointResults.From(await service.CancelAsync(id, request, ct), d => d));

        // Several reservations back to stock with one reason; each is released and audited on its own.
        allocations.MapPost("/release-batch", async (ReleaseAllocationsRequest request, CompatibilityService service, CancellationToken ct) =>
            EndpointResults.From(await service.ReleaseAllocationsAsync(request, ct), d => d))
            .RequirePermission(PermissionCodes.CompatibilityAllocate);

        allocations.MapPost("/{id:long}/release", async (long id, ReleaseAllocationRequest request, CompatibilityService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.ReleaseAllocationAsync(id, request.Reason, ct), a => AllocationDto.From(a)))
            .RequirePermission(PermissionCodes.CompatibilityAllocate);
    }
}
