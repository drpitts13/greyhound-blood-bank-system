using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class SupplierOrderEndpoints
{
    public static void MapSupplierOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory/supplier-orders").WithTags("Inventory: Supplier orders").RequireAuthenticatedUser();

        group.MapGet("/", async (SupplierOrderService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        group.MapGet("/{id:long}", async (long id, SupplierOrderService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound(new { error = "Order not found." }));

        group.MapPost("/", async (CreateSupplierOrderRequest request, SupplierOrderService service, CancellationToken ct) =>
            ToResult(await service.CreateFromShortfallAsync(request, ct)))
            .RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPut("/{id:long}", async (long id, UpdateSupplierOrderRequest request, SupplierOrderService service, CancellationToken ct) =>
            ToResult(await service.UpdateAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/{id:long}/submit", async (long id, SubmitSupplierOrderRequest request, SupplierOrderService service, CancellationToken ct) =>
            ToResult(await service.SubmitAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/{id:long}/receive", async (long id, ReceiveSupplierOrderRequest request, SupplierOrderService service, CancellationToken ct) =>
            ToResult(await service.ReceiveAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/{id:long}/follow-up", async (long id, SupplierOrderFollowUpRequest request, SupplierOrderService service, CancellationToken ct) =>
            ToResult(await service.RecordFollowUpAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/{id:long}/cancel", async (long id, CancelSupplierOrderRequest request, SupplierOrderService service, CancellationToken ct) =>
            ToResult(await service.CancelAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.InventoryReceive);

        group.MapGet("/{id:long}/export", async (long id, string format, SupplierOrderService service, CancellationToken ct) =>
        {
            var result = await service.ExportAsync(id, format, ct);
            return result.Succeeded
                ? Results.File(result.Value!.Bytes, result.Value.ContentType, result.Value.FileName)
                : Results.BadRequest(new { error = result.Error });
        });
    }

    private static IResult ToResult<T>(OperationResult<T> result) =>
        result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
}
