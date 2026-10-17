using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class StockParEndpoints
{
    public static void MapStockParEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory/par").WithTags("Inventory: Par").RequireAuthenticatedUser();
        group.MapGet("/", async (StockParService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));
        group.MapPost("/", async (SaveStockParRequest request, StockParService service, CancellationToken ct) =>
        {
            var result = await service.SaveAsync(request, ct);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.AdminConfigEdit);
    }
}
