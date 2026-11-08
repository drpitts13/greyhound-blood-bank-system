using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Search;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>Top-bar "scan anything" lookup. POST keeps MRNs and names out of URLs and request logs.</summary>
public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        // The signed-in user's recently opened charts, shown when the lookup is focused with nothing typed.
        app.MapGet("/api/lookup/recent", async (UniversalLookupService service, CancellationToken ct) =>
                Results.Ok(await service.RecentPatientsAsync(ct)))
            .WithTags("Lookup")
            .RequireAuthenticatedUser();

        app.MapPost("/api/lookup", async (LookupRequest request, UniversalLookupService service, CancellationToken ct) =>
                Results.Ok(await service.LookupAsync(request.Query, ct)))
            .WithTags("Lookup")
            .RequireAuthenticatedUser();
    }
}
