using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class PatientWorkspaceEndpoints
{
    public static void MapPatientWorkspaceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/patients/{patientId:long}")
            .WithTags("Patient Workspace")
            .RequireAuthenticatedUser();

        group.MapGet("/encounters", async (long patientId, EncounterService service, CancellationToken ct) =>
        {
            var list = await service.ListByPatientAsync(patientId, ct);
            return Results.Ok(list.Select(EncounterDto.From));
        });

        group.MapPost("/encounters", async (long patientId, CreateEncounterRequest request, EncounterService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(patientId, request, ct);
            return result.Succeeded
                ? Results.Created($"/api/patients/{patientId}/encounters/{result.Value!.Id}", EncounterDto.From(result.Value))
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.PatientWrite);

        group.MapPut("/encounters/{encounterId:long}", async (
            long patientId,
            long encounterId,
            UpdateEncounterRequest request,
            EncounterService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(patientId, encounterId, request, ct);
            return result.Succeeded
                ? Results.Ok(EncounterDto.From(result.Value!))
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.PatientWrite);

        group.MapGet("/orders", async (
            long patientId,
            long? encounterId,
            string? category,
            bool? activeOnly,
            string? q,
            OrderService service,
            CancellationToken ct) =>
        {
            OrderCategory? cat = null;
            if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<OrderCategory>(category, true, out var parsed))
            {
                cat = parsed;
            }

            var list = await service.ListByPatientAsync(patientId, encounterId, cat, activeOnly, q, ct);
            return Results.Ok(list);
        });

        group.MapPost("/orders", async (long patientId, CreateOrderRequest request, OrderService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(patientId, request, ct);
            return result.Succeeded
                ? Results.Created($"/api/patients/{patientId}/orders/{result.Value!.Id}", new { result.Value.Id })
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.PatientWrite);

        group.MapPut("/orders/{orderId:long}", async (
            long patientId,
            long orderId,
            UpdateOrderRequest request,
            OrderService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(patientId, orderId, request, ct);
            return result.Succeeded
                ? Results.Ok(new { result.Value!.Id, result.Value.Status })
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.PatientWrite);

        group.MapPost("/orders/{orderId:long}/cancel", async (
            long patientId,
            long orderId,
            CancelOrderRequest request,
            OrderService service,
            CancellationToken ct) =>
        {
            var result = await service.CancelAsync(patientId, orderId, request, ct);
            return result.Succeeded
                ? Results.Ok(new { result.Value!.Id, result.Value.Status })
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.PatientWrite);

        group.MapPut("/orders/{orderId:long}/specimen", async (
            long patientId,
            long orderId,
            LinkOrderSpecimenRequest request,
            OrderService service,
            CancellationToken ct) =>
        {
            var result = await service.LinkSpecimenAsync(patientId, orderId, request, ct);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.PatientWrite);

        group.MapGet("/product-history", async (
            long patientId,
            long? encounterId,
            PatientProductHistoryService service,
            CancellationToken ct) =>
        {
            var list = await service.ListByPatientAsync(patientId, encounterId, ct);
            return Results.Ok(list);
        });

        group.MapGet("/allocations", async (
            long patientId,
            PatientAllocationService service,
            CancellationToken ct) =>
        {
            var list = await service.ListActiveAsync(patientId, ct);
            return Results.Ok(list);
        });

        group.MapGet("/compatible-units", async (
            long patientId,
            PatientAllocationService service,
            CancellationToken ct) =>
        {
            var result = await service.ListCompatibleUnitsAsync(patientId, ct);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        group.MapGet("/crossmatch-tests", async (PatientAllocationService service, CancellationToken ct) =>
        {
            var list = await service.ListCrossmatchTestsAsync(ct);
            return Results.Ok(list);
        });

        group.MapPost("/allocations", async (
            long patientId,
            AllocatePatientUnitRequest request,
            PatientAllocationService service,
            CancellationToken ct) =>
        {
            var result = await service.AllocateAsync(patientId, request, ct);
            if (!result.Succeeded || result.Value is null)
            {
                return result.Evaluation is not null
                    ? EndpointResults.FromEvaluation(result, r => r)
                    : Results.BadRequest(new { error = result.Error });
            }

            return Results.Created(
                $"/api/patients/{patientId}/allocations/{result.Value.Allocation.AllocationId}",
                result.Value);
        }).RequirePermission(PermissionCodes.CompatibilityAllocate);
    }
}
