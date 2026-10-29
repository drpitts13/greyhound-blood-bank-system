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
    }

    private static IResult ToResult<T>(Application.Common.OperationResult<T> result) =>
        result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
}
