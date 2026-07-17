using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class TestWorklistEndpoints
{
    public static void MapTestWorklistEndpoints(this WebApplication app)
    {
        var patientGroup = app.MapGroup("/api/patients/{patientId:long}/test-worklist")
            .WithTags("Test Worklist")
            .RequireAuthenticatedUser();

        patientGroup.MapGet("/", async (
            long patientId,
            string? status,
            string? q,
            TestWorklistService service,
            CancellationToken ct) =>
        {
            var filter = ParseFilter(status);
            var items = await service.ListForPatientAsync(patientId, filter, q, ct);
            return Results.Ok(items);
        }).RequirePermission(PermissionCodes.ResultEnter);

        app.MapGet("/api/test-worklist/pending", async (TestWorklistService service, CancellationToken ct) =>
        {
            var items = await service.ListPendingGlobalAsync(ct);
            return Results.Ok(items);
        })
            .WithTags("Test Worklist")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.ResultEnter);

        app.MapGet("/api/specimens/{specimenId:long}/test-worklist", async (
            long specimenId,
            string? status,
            TestWorklistService service,
            CancellationToken ct) =>
        {
            var filter = ParseFilter(status);
            var items = await service.ListForSpecimenAsync(specimenId, filter, ct);
            return Results.Ok(items);
        })
            .WithTags("Test Worklist")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.ResultEnter);
    }

    private static TestWorklistFilter ParseFilter(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "completed" => TestWorklistFilter.Completed,
        "all" or "both" => TestWorklistFilter.All,
        _ => TestWorklistFilter.Pending
    };
}
