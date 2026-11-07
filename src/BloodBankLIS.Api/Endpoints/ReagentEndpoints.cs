using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class ReagentEndpoints
{
    public static void MapReagentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reagents").WithTags("Reagents").RequireAuthenticatedUser();

        group.MapGet("/", async (ReagentQcService service, CancellationToken ct) =>
            Results.Ok(await service.ListReagentsAsync(ct)));

        group.MapPost("/", async (SaveReagentRequest request, ReagentQcService service, CancellationToken ct) =>
            ToResult(await service.SaveReagentAsync(request, ct)))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapPut("/{id:long}/tests", async (long id, SetReagentTestsRequest request, ReagentQcService service, CancellationToken ct) =>
            ToResult(await service.SetReagentTestsAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.ResultEnter);

        // Lot lookback: the results recorded as tested with a lot (for a lot that failed QC or was recalled).
        group.MapGet("/lots/{id:long}/results", async (long id, ReagentQcService service, CancellationToken ct) =>
            ToResult(await service.ResultsForLotAsync(id, ct)));

        group.MapGet("/results/{resultId:long}/lots", async (long resultId, ReagentQcService service, CancellationToken ct) =>
            Results.Ok(await service.LotsForResultAsync(resultId, ct)));

        group.MapGet("/lots", async (ReagentQcService service, CancellationToken ct) =>
            Results.Ok(await service.ListLotsAsync(ct)));

        group.MapPost("/lots", async (ReceiveReagentLotRequest request, ReagentQcService service, CancellationToken ct) =>
            ToResult(await service.ReceiveLotAsync(request, ct)))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapPost("/lots/{id:long}/open", async (long id, ReagentQcService service, CancellationToken ct) =>
            ToResult(await service.OpenLotAsync(id, ct)))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapPost("/lots/{id:long}/close", async (long id, ReagentQcService service, CancellationToken ct) =>
            ToResult(await service.CloseLotAsync(id, ct)))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapPost("/qc", async (RecordReagentQcRequest request, ReagentQcService service, CancellationToken ct) =>
            ToResult(await service.RecordQcAsync(request, ct)))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapPost("/qc/{id:long}/review", async (long id, ReagentQcService service, CancellationToken ct) =>
            ToResult(await service.ReviewAsync(id, ct)))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapGet("/worklist", async (ReagentQcService service, CancellationToken ct) =>
            Results.Ok(await service.ListWorkAsync(ct)));

        group.MapGet("/qc", async (int? days, ReagentQcService service, CancellationToken ct) =>
            Results.Ok(await service.ListRecentQcAsync(days ?? 14, ct)));
    }

    private static IResult ToResult<T>(Application.Common.OperationResult<T> result) =>
        result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
}
