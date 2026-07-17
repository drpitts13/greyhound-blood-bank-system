using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/billing").WithTags("Billing").RequireAuthenticatedUser();

        // Charge review queue (pending charges awaiting billing-staff action).
        group.MapGet("/charges", async (BillingService service, CancellationToken ct) =>
        {
            var queue = await service.GetReviewQueueAsync(ct);
            return Results.Ok(queue.Select(BillingEventDto.From));
        }).RequirePermission(PermissionCodes.BillingReview);

        group.MapPost("/charges/{id:long}/review", async (long id, BillingService service, CancellationToken ct) =>
            EndpointResults.From(await service.ReviewAsync(id, ct), BillingEventDto.From))
            .RequirePermission(PermissionCodes.BillingReview);

        group.MapPost("/charges/{id:long}/cancel", async (long id, CancelChargeRequest request, BillingService service, CancellationToken ct) =>
            EndpointResults.From(await service.CancelAsync(id, request.Reason, ct), BillingEventDto.From))
            .RequirePermission(PermissionCodes.BillingCancel);

        group.MapPost("/charges/{id:long}/export", async (long id, BillingService service, CancellationToken ct) =>
            EndpointResults.From(await service.ExportAsync(id, ct), BillingEventDto.From))
            .RequirePermission(PermissionCodes.BillingExport);

        // Manual (re)capture entry points; capture is idempotent on the dedupe key.
        group.MapPost("/capture/result/{resultId:long}", async (long resultId, BillingService service, CancellationToken ct) =>
            EndpointResults.From(await service.CaptureForResultAsync(resultId, ct),
                events => events.Select(BillingEventDto.From)))
            .RequirePermission(PermissionCodes.BillingReview);

        group.MapPost("/capture/issue/{issueId:long}", async (long issueId, BillingService service, CancellationToken ct) =>
            EndpointResults.From(await service.CaptureForIssueAsync(issueId, ct),
                events => events.Select(BillingEventDto.From)))
            .RequirePermission(PermissionCodes.BillingReview);
    }
}
