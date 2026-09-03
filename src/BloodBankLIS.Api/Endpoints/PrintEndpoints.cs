using BloodBankLIS.Api.Auth;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Printing;

namespace BloodBankLIS.Api.Endpoints;

public sealed record ReprintRequest(string Reason);

public static class PrintEndpoints
{
    public static void MapPrintEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/print").WithTags("Printing").RequireAuthenticatedUser();

        group.MapPost("/specimen-labels/{specimenId:long}", async (long specimenId, PrintRequest? request, PrintService service, CancellationToken ct) =>
            EndpointResults.Created(await service.PrintSpecimenLabelAsync(specimenId, request ?? new PrintRequest(), ct),
                j => ($"/api/print/jobs/{j.Id}", (object)PrintJobDto.Full(j))))
            .RequirePermission(PermissionCodes.PrintLabel);

        group.MapPost("/compatibility-tags/{issueId:long}", async (long issueId, PrintRequest? request, PrintService service, CancellationToken ct) =>
            EndpointResults.Created(await service.PrintCompatibilityTagAsync(issueId, request ?? new PrintRequest(), ct),
                j => ($"/api/print/jobs/{j.Id}", (object)PrintJobDto.Full(j))))
            .RequirePermission(PermissionCodes.PrintLabel);

        group.MapPost("/component-labels/{unitId:long}", async (long unitId, PrintRequest? request, PrintService service, CancellationToken ct) =>
            EndpointResults.Created(await service.PrintComponentLabelAsync(unitId, request ?? new PrintRequest(), ct),
                j => ($"/api/print/jobs/{j.Id}", (object)PrintJobDto.Full(j))))
            .RequirePermission(PermissionCodes.PrintLabel);

        group.MapPost("/jobs/{id:long}/reprint", async (long id, ReprintRequest request, PrintService service, CancellationToken ct) =>
            EndpointResults.From(await service.ReprintAsync(id, request.Reason, ct), j => PrintJobDto.Full(j)))
            .RequirePermission(PermissionCodes.PrintReprint);

        group.MapGet("/jobs", async (PrintService service, CancellationToken ct) =>
        {
            var jobs = await service.ListAsync(ct);
            return Results.Ok(jobs.OrderByDescending(j => j.CreatedUtc).Select(PrintJobDto.Summary));
        });

        group.MapGet("/jobs/{id:long}", async (long id, PrintService service, CancellationToken ct) =>
        {
            var job = await service.GetAsync(id, ct);
            return job is null ? Results.NotFound() : Results.Ok(PrintJobDto.Full(job));
        });
    }
}
