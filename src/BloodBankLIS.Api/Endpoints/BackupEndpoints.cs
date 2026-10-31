using BloodBankLIS.Api.Auth;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Api.Endpoints;

public static class BackupEndpoints
{
    public static void MapBackupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/backups").WithTags("Admin: Backups").RequireAuthenticatedUser();

        group.MapGet("/", async (DatabaseBackupService service, CancellationToken ct) =>
            Results.Ok(await service.GetStatusAsync(ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("/", async (DatabaseBackupService service, CancellationToken ct) =>
        {
            var result = await service.RunAsync(DatabaseBackupRule.ManualTrigger, ct);
            await service.SyncOverdueAlertAsync(ct);
            return Results.Ok(result);
        }).RequirePermission(PermissionCodes.AdminConfigEdit);
    }
}
