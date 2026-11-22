using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class MinimumStockEndpoints
{
    public static void MapMinimumStockEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory/minimum-stock").WithTags("Inventory: Minimum stock").RequireAuthenticatedUser();
        group.MapGet("/", async (MinimumStockService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));
        group.MapPost("/", async (SaveMinimumStockRequest request, MinimumStockService service, CancellationToken ct) =>
        {
            var result = await service.SaveAsync(request, ct);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.AdminConfigEdit);
    }
}
