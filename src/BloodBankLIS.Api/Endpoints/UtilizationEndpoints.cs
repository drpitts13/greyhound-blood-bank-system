using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class UtilizationEndpoints
{
    public static void MapUtilizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/utilization").WithTags("Utilization").RequireAuthenticatedUser();

        group.MapGet("/", async (DateTime? fromUtc, DateTime? toUtc, UtilizationService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(fromUtc, toUtc, ct)))
            .RequirePermission(PermissionCodes.ReportRun);
    }
}
