using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class StorageTemperatureEndpoints
{
    public static void MapStorageTemperatureEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/storage-temps").WithTags("Storage temperatures").RequireAuthenticatedUser();

        group.MapGet("/locations", async (StorageTemperatureService service, CancellationToken ct) =>
            Results.Ok(await service.ListLocationsAsync(ct)));

        group.MapGet("/readings", async (long? locationId, int? days, StorageTemperatureService service, CancellationToken ct) =>
            Results.Ok(await service.ListReadingsAsync(locationId, days ?? 7, ct)));

        group.MapPost("/readings", async (RecordStorageTemperatureRequest request, StorageTemperatureService service, CancellationToken ct) =>
            {
                var result = await service.RecordAsync(request, ct);
                return result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
            })
            .RequirePermission(PermissionCodes.InventoryReceive);

        group.MapGet("/devices", async (StorageTemperatureService service, CancellationToken ct) =>
            Results.Ok(await service.ListDevicesAsync(ct)));

        group.MapGet("/report", async (long? locationId, DateTime fromUtc, DateTime toUtc, bool? excursionsOnly, StorageTemperatureService service, CancellationToken ct) =>
            {
                var result = await service.ReportAsync(
                    locationId,
                    DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
                    DateTime.SpecifyKind(toUtc, DateTimeKind.Utc),
                    excursionsOnly ?? false,
                    ct);
                return result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
            });

        group.MapGet("/excursions", async (StorageTemperatureService service, CancellationToken ct) =>
            Results.Ok(await service.ListOpenExcursionsAsync(ct)));
    }
}
