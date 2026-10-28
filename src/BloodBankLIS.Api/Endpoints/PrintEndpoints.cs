using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Printing;
using BloodBankLIS.Printing.Delivery;

namespace BloodBankLIS.Api.Endpoints;

public sealed record ReprintRequest(string Reason);

public sealed record PrepareCompatibilityTagRequest(
    long UnitId,
    long PatientId,
    IssueType IssueType = IssueType.Standard,
    LabelFormat Format = LabelFormat.Pdf,
    string? TemplateCode = null,
    string? TargetPrinter = null);

public static class PrintEndpoints
{
    public static void MapPrintEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/print").WithTags("Printing").RequireAuthenticatedUser();

        group.MapPost("/specimen-labels/{specimenId:long}", async (long specimenId, PrintRequest? request, PrintService service, CancellationToken ct) =>
            EndpointResults.Created(await service.PrintSpecimenLabelAsync(specimenId, request ?? new PrintRequest(), ct),
                j => ($"/api/print/jobs/{j.Id}", (object)PrintJobDto.Full(j))))
            .RequirePermission(PermissionCodes.PrintLabel);

        group.MapGet("/ptag-id-required", async (LabelLayoutAdminService layouts, CancellationToken ct) =>
            Results.Ok(new { required = await layouts.RequiresPtagIdAsync(ct) }))
            // Issuers must scan the P-tag ID even when someone else prints the tag.
            .RequireAnyPermission(PermissionCodes.PrintLabel, PermissionCodes.IssueCreate, PermissionCodes.TransfusionDocument);

        group.MapPost("/compatibility-tags/prepare", async (PrepareCompatibilityTagRequest body, PrintService service, CancellationToken ct) =>
            EndpointResults.Created(
                await service.PrintPreparedCompatibilityTagAsync(
                    body.UnitId,
                    body.PatientId,
                    body.IssueType,
                    new PrintRequest(body.Format, body.TemplateCode, body.TargetPrinter),
                    ct),
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

        group.MapGet("/jobs/{id:long}/pdf", async (long id, PrintService service, CancellationToken ct) =>
        {
            var pdf = await service.GetPdfAsync(id, ct);
            return pdf is null ? Results.NotFound() : Results.File(pdf, "application/pdf");
        });

        group.MapGet("/pdf-printers", async (PrinterCatalogAdminService printers, CancellationToken ct) =>
            Results.Ok(await printers.ListActivePdfChoicesAsync(ct)))
            .RequirePermission(PermissionCodes.PrintLabel);

        group.MapGet("/zebra-printers", async (PrinterCatalogAdminService printers, CancellationToken ct) =>
            Results.Ok(await printers.ListActiveZebraChoicesAsync(ct)))
            .RequirePermission(PermissionCodes.PrintLabel);

        group.MapPost("/jobs/{id:long}/deliver", async (long id, DeliverPrintRequest request, PrintDeliveryService delivery, CancellationToken ct) =>
            EndpointResults.From(await delivery.DeliverAsync(id, request, ct), j => PrintJobDto.Full(j)))
            .RequirePermission(PermissionCodes.PrintLabel);
    }
}
