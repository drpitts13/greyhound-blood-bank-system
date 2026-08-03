using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class IsbtEndpoints
{
    public static void MapIsbtEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/isbt").WithTags("ISBT 128").RequireAuthenticatedUser();

        group.MapPost("/parse", async (ParseIsbtInputRequest request, IsbtParsingService service, CancellationToken ct) =>
        {
            var result = await service.ParseAsync(request.Value, ct);
            return Results.Ok(result);
        }).RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/scan-sessions", async (StartScanSessionRequest request, ScanSessionService service, CancellationToken ct) =>
        {
            var result = await service.StartAsync(request, ct);
            return result.Succeeded
                ? Results.Created($"/api/isbt/scan-sessions/{result.Value!.SessionKey}", result.Value)
                : Results.BadRequest(new { error = result.Error });
        }).RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/scan-sessions/scans", async (AddScanRequest request, ScanSessionService service, CancellationToken ct) =>
        {
            var result = await service.AddScanAsync(request, ct);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.UnprocessableEntity(new { error = result.Error, code = ExtractCode(result.Error) });
        }).RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/scan-sessions/complete", async (CompleteScanSessionRequest request, ScanSessionService service, CancellationToken ct) =>
        {
            var result = await service.CompleteAsync(request, ct);
            return result.Succeeded
                ? Results.Created($"/api/inventory/units/{result.Unit!.Id}", BloodUnitDto.From(result.Unit))
                : ToInventoryFailure(result);
        }).RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/manual-entry", async (ManualComponentEntryRequest request, ManualComponentEntryService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.Succeeded
                ? Results.Created($"/api/inventory/units/{result.Unit!.Id}", BloodUnitDto.From(result.Unit))
                : ToInventoryFailure(result);
        }).RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/identity-corrections", async (CorrectIdentityRequest request, ComponentIdentityCorrectionService service, CancellationToken ct) =>
        {
            var result = await service.CorrectAsync(request, ct: ct);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.UnprocessableEntity(new { error = result.Error, code = ExtractCode(result.Error) });
        }).RequirePermission(PermissionCodes.InventoryCorrectIdentity);

        group.MapPost("/units/{id:long}/recall", async (long id, DiscardUnitRequest request, InventoryService service, CancellationToken ct) =>
        {
            var result = await service.RecallAsync(id, request.Reason, ct);
            return result.Succeeded
                ? Results.Ok(BloodUnitDto.From(result.Unit!))
                : ToInventoryFailure(result);
        }).RequirePermission(PermissionCodes.InventoryRecall);

        group.MapPost("/units/{id:long}/quarantine", async (long id, DiscardUnitRequest request, InventoryService service, CancellationToken ct) =>
        {
            var result = await service.QuarantineAsync(id, request.Reason, ct);
            return result.Succeeded
                ? Results.Ok(BloodUnitDto.From(result.Unit!))
                : ToInventoryFailure(result);
        }).RequirePermission(PermissionCodes.InventoryRelease);
    }

    private static string? ExtractCode(string? error)
    {
        if (string.IsNullOrEmpty(error)) return null;
        var idx = error.IndexOf(':');
        return idx > 0 ? error[..idx].Trim() : null;
    }

    private static IResult ToInventoryFailure(InventoryActionResult result)
    {
        if (result.Evaluation is not null)
        {
            return Results.UnprocessableEntity(new
            {
                blocked = true,
                hardStops = result.Evaluation.HardStops.Select(r => new { r.Code, r.Message }),
                warnings = result.Evaluation.Warnings.Select(r => new { r.Code, r.Message })
            });
        }

        return Results.UnprocessableEntity(new { error = result.Error, code = ExtractCode(result.Error) });
    }
}
