using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Compliance;

namespace BloodBankLIS.Api.Endpoints;

public static class AlertEndpoints
{
    public static void MapAlertEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/alerts").WithTags("Alerts").RequireAuthenticatedUser();

        group.MapGet("/", async (bool? openOnly, AlertService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(openOnly ?? false, ct)));

        group.MapGet("/count", async (AlertService service, CancellationToken ct) =>
            Results.Ok(new { open = await service.CountOpenAsync(ct) }));

        group.MapPost("/{id:long}/acknowledge", async (long id, AcknowledgeAlertRequest request, AlertService service, CancellationToken ct) =>
        {
            var result = await service.AcknowledgeAsync(id, request, ct);
            return result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
        });
    }
}
