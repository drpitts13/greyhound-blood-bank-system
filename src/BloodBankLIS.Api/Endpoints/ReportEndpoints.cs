using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthenticatedUser();

        group.MapGet("/", async (ReportService service, CancellationToken ct) =>
            EndpointResults.From(await service.ListAsync(ct), rows => rows))
            .RequireAnyPermission(PermissionCodes.ReportRun, PermissionCodes.ReportDesign);

        group.MapGet("/sources", (ReportService service) => Results.Ok(service.Sources()))
            .RequirePermission(PermissionCodes.ReportDesign);

        group.MapGet("/{id:long}", async (long id, ReportService service, CancellationToken ct) =>
            EndpointResults.From(await service.GetAsync(id, ct), row => row))
            .RequireAnyPermission(PermissionCodes.ReportRun, PermissionCodes.ReportDesign);

        group.MapPost("/", async (SaveReportRequest request, ReportService service, CancellationToken ct) =>
            EndpointResults.Created(await service.SaveAsync(null, request, ct), dto => ($"/api/reports/{dto.Id}", (object)dto)))
            .RequirePermission(PermissionCodes.ReportDesign);

        group.MapPut("/{id:long}", async (long id, SaveReportRequest request, ReportService service, CancellationToken ct) =>
            EndpointResults.From(await service.SaveAsync(id, request, ct), row => row))
            .RequirePermission(PermissionCodes.ReportDesign);

        group.MapPost("/{id:long}/clone", async (long id, ReportService service, CancellationToken ct) =>
            EndpointResults.Created(await service.CloneAsync(id, ct), dto => ($"/api/reports/{dto.Id}", (object)dto)))
            .RequirePermission(PermissionCodes.ReportDesign);

        group.MapDelete("/{id:long}", async (long id, ReportService service, CancellationToken ct) =>
            EndpointResults.From(await service.DeleteAsync(id, ct), _ => new { deleted = true }))
            .RequirePermission(PermissionCodes.ReportDesign);

        group.MapPost("/validate-sql", (SqlTextRequest request, ReportService service) =>
        {
            var result = service.ValidateSql(request.Sql);
            return result.Severity == RuleSeverity.HardStop
                ? Results.BadRequest(new { error = result.Message })
                : Results.Ok(new { ok = true });
        }).RequirePermission(PermissionCodes.ReportDesign);

        group.MapPost("/preview", async (PreviewReportRequest request, ReportService service, CancellationToken ct) =>
            EndpointResults.From(await service.PreviewAsync(request, ct), row => row))
            .RequirePermission(PermissionCodes.ReportDesign);

        group.MapPost("/{id:long}/run", async (long id, RunReportRequest? request, ReportService service, CancellationToken ct) =>
            EndpointResults.From(await service.RunAsync(id, request?.Parameters, ct), row => row))
            .RequireAnyPermission(PermissionCodes.ReportRun, PermissionCodes.ReportDesign);

        group.MapPost("/{id:long}/export", async (long id, string format, RunReportRequest? request, ReportService service, CancellationToken ct) =>
        {
            var result = await service.ExportAsync(id, format, request?.Parameters, ct);
            IResult response = result.Succeeded
                ? Results.File(result.Value!.Bytes, result.Value.ContentType, result.Value.FileName)
                : EndpointResults.From(result, file => file);
            return response;
        }).RequireAnyPermission(PermissionCodes.ReportRun, PermissionCodes.ReportDesign);
    }

    private sealed record SqlTextRequest(string? Sql);
}
