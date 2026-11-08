using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>Blood orders to prepare: open product orders with units still to reserve, across patients.</summary>
public static class BloodOrderEndpoints
{
    public static void MapBloodOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/api/blood-orders/to-prepare", async (BloodOrderWorklistService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(ct)))
            .WithTags("Orders")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.CompatibilityAllocate);

        // The reserve form's line for the chosen order: how many more units it needs.
        app.MapGet("/api/blood-orders/{orderId:long}/progress", async (long orderId, BloodOrderWorklistService service, CancellationToken ct) =>
                await service.GetProgressAsync(orderId, ct) is { } progress ? Results.Ok(progress) : Results.NotFound())
            .WithTags("Orders")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.CompatibilityAllocate);
    }
}
