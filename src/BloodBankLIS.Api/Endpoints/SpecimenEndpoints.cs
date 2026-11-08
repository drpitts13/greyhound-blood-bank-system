using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>ReasonCode from the rejection-reason catalog; Reason is the older free-text field, read as the code.</summary>
public sealed record RejectSpecimenRequest(string? Reason = null, string? ReasonCode = null, string? Comment = null);

public static class SpecimenEndpoints
{
    public static void MapSpecimenEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/specimens").WithTags("Specimens").RequireAuthenticatedUser();

        group.MapGet("/{id:long}", async (long id, SpecimenService service, CancellationToken ct) =>
        {
            var specimen = await service.GetAsync(id, ct);
            return specimen is null ? Results.NotFound() : Results.Ok(specimen);
        });

        // Same access as reading by id; lets screens take the accession number printed on the tube.
        group.MapGet("/by-accession/{accession}", async (string accession, SpecimenService service, CancellationToken ct) =>
        {
            var specimen = await service.GetByAccessionAsync(accession, ct);
            return specimen is null
                ? Results.NotFound(new { error = $"No specimen with accession number {accession.Trim()}." })
                : Results.Ok(specimen);
        });

        group.MapPost("/", async (AccessionSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.AccessionAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return Results.Created($"/api/specimens/{result.Value.Id}", dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPut("/{id:long}", async (long id, UpdateSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null
                ? Results.NotFound()
                : EndpointResults.From(
                    OperationResult<SpecimenDto>.Ok(dto, result.Warnings),
                    payload => payload!);
        }).RequirePermission(PermissionCodes.SpecimenEdit);

        // Read-only history of one tube, same access as reading the specimen.
        // Put a tube away by scan; the earlier place is kept (SpecimenStorageEvent) and the move audited.
        group.MapGet("/stored", async (string? location, SpecimenStorageService service, CancellationToken ct) =>
            Results.Ok(await service.ListInPlaceAsync(location, ct)));

        group.MapPost("/store", async (StoreSpecimenRequest request, SpecimenStorageService service, CancellationToken ct) =>
            EndpointResults.From(await service.StoreAsync(request, ct), d => d))
            .RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapGet("/{id:long}/timeline", async (long id, SpecimenTimelineService service, CancellationToken ct) =>
        {
            var timeline = await service.GetAsync(id, ct);
            return timeline is null ? Results.NotFound(new { error = $"No specimen {id}." }) : Results.Ok(timeline);
        });

        group.MapGet("/pending", async (SpecimenService service, CancellationToken ct) =>
            Results.Ok(await service.ListPendingReceiptAsync(ct)))
            .RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/{id:long}/relabel", async (long id, RelabelSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.RelabelAsync(id, request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenEdit);

        // Barcode-first receipt: the scanned accession or label barcode picks the specimen.
        group.MapPost("/receive-scan", async (ReceiveSpecimenScanRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.ReceiveByScanAsync(request.Scan, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/accept-scan", async (ReceiveSpecimenScanRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.AcceptByScanAsync(request.Scan, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/{id:long}/receive", async (long id, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.ReceiveAsync(id, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/{id:long}/collector", async (long id, RecordSpecimenCollectorRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.RecordCollectorAsync(id, request.Collector, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/{id:long}/accept", async (long id, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.AcceptAsync(id, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.SpecimenAccession);

        group.MapPost("/{id:long}/reject", async (long id, RejectSpecimenRequest request, SpecimenService service, CancellationToken ct) =>
        {
            var result = await service.RejectAsync(id, request.ReasonCode ?? request.Reason, request.Comment, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            var dto = await service.GetAsync(result.Value!.Id, ct);
            return dto is null
                ? Results.NotFound()
                : EndpointResults.From(
                    OperationResult<SpecimenDto>.Ok(dto, result.Warnings),
                    payload => payload!);
        }).RequirePermission(PermissionCodes.SpecimenReject);

        app.MapGet("/api/patients/{patientId:long}/specimens", async (long patientId, SpecimenService service, CancellationToken ct) =>
        {
            var specimens = await service.GetByPatientAsync(patientId, ct);
            return Results.Ok(specimens);
        }).WithTags("Specimens");

        app.MapGet("/api/patients/{patientId:long}/specimen-expiration", async (
            long patientId, string? specimenType, DateTime collectedUtc, int? validityHours, SpecimenService service, CancellationToken ct) =>
        {
            var preview = await service.PreviewExpirationAsync(patientId, specimenType, collectedUtc, ct, validityHours);
            return preview.Succeeded ? Results.Ok(preview.Value) : Results.NotFound(new { error = preview.Error });
        }).WithTags("Specimens").RequireAuthenticatedUser();
    }
}
