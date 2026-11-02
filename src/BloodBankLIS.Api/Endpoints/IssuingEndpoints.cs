using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class IssuingEndpoints
{
    public const string OverrideSignatureHeader = "X-Esignature-Id";
    private const string OverrideSignatureAction = "IssueOverride";

    public static void MapIssuingEndpoints(this WebApplication app)
    {
        var issues = app.MapGroup("/api/issues").WithTags("Issuing");

        // Runs the full issue gate; a HardStop returns 422, a Warning-only block is
        // 422 with overridable=true until an authorized override is supplied.
        issues.MapPost("/", async (
            IssueUnitRequest request,
            IssuingService service,
            BillingService billing,
            ISignatureService signatures,
            HttpContext http,
            CancellationToken ct) =>
        {
            // An override path requires reason + a valid electronic signature by the
            // current user before the gate is even attempted (docs/safety-rules.md;
            // docs/validation-plan.md 2.6).
            if (IsOverrideAttempt(request))
            {
                if (string.IsNullOrWhiteSpace(request.OverrideReason))
                {
                    return Results.BadRequest(new { error = "An override reason is required." });
                }

                var signatureValid = await TryValidateOverrideSignatureAsync(http, signatures, ct);
                if (!signatureValid)
                {
                    return Results.Problem(
                        title: "Electronic signature required",
                        detail: $"Supply a valid '{OverrideSignatureAction}' signature id via the '{OverrideSignatureHeader}' header.",
                        statusCode: StatusCodes.Status403Forbidden);
                }
            }

            var result = await service.IssueUnitAsync(request, ct);
            if (result.Succeeded)
            {
                await billing.CaptureForIssueAsync(result.Value!.Id, ct);
                var header = http.Request.Headers[OverrideSignatureHeader].ToString();
                if (long.TryParse(header, out var signatureId))
                {
                    await signatures.ConsumeAsync(signatureId, ct);
                }
            }

            return EndpointResults.CreatedEvaluation(result, i => ($"/api/issues/{i.Id}", (object)IssueDto.From(i)));
        }).RequirePermission(PermissionCodes.IssueCreate);

        // Massive transfusion protocol: activate (who called it and why), board, close. OCD-012 permission.
        app.MapPost("/api/patients/{patientId:long}/mtp", async (long patientId, ActivateMtpRequest request, MtpActivationService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.ActivateAsync(patientId, request, ct), d => d))
            .WithTags("Issuing")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.IssueEmergencyRelease);
        app.MapGet("/api/patients/{patientId:long}/mtp", async (long patientId, MtpActivationService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListForPatientAsync(patientId, ct)))
            .WithTags("Issuing")
            .RequireAuthenticatedUser();
        app.MapGet("/api/mtp/active", async (MtpActivationService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListActiveAsync(ct)))
            .WithTags("Issuing")
            .RequireAuthenticatedUser();
        // Facility packs (OCD-012): active ones for the desk; the admin catalog below.
        app.MapGet("/api/mtp/packs", async (MtpPackAdminService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListActiveAsync(ct)))
            .WithTags("Issuing")
            .RequireAuthenticatedUser();
        var packs = app.MapGroup("/api/admin/mtp-packs").WithTags("Admin: MTP Packs").RequireAuthenticatedUser();
        packs.MapGet("", async (MtpPackAdminService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);
        packs.MapPost("", async (SaveMtpPackRequest req, MtpPackAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);
        packs.MapPut("/{id:long}", async (long id, SaveMtpPackRequest req, MtpPackAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);
        packs.MapPost("/{id:long}/activate", async (long id, MtpPackAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, true, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
        packs.MapPost("/{id:long}/deactivate", async (long id, MtpPackAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, false, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        // Physical packs sent during a protocol, and the printable pack report.
        app.MapPost("/api/mtp/{id:long}/packs", async (long id, PackMtpRequest request, MtpPackingService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.PackAsync(id, request, ct), d => d))
            .WithTags("Issuing")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.IssueCreate);
        app.MapGet("/api/mtp/{id:long}/packs", async (long id, MtpPackingService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListForActivationAsync(id, ct)))
            .WithTags("Issuing")
            .RequireAuthenticatedUser();
        app.MapGet("/api/mtp/pack-records/{id:long}/report", async (long id, MtpPackingService svc, CancellationToken ct) =>
                await svc.GetReportAsync(id, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .WithTags("Issuing")
            .RequireAuthenticatedUser();
        app.MapPost("/api/mtp/pack-records/{id:long}/printed", async (long id, MtpPackingService svc, CancellationToken ct) =>
                await svc.RecordPrintedAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .WithTags("Issuing")
            .RequireAuthenticatedUser();

        // Coolers and other transport vessels: active ones for the desk; the admin catalog below.
        app.MapGet("/api/transport-containers", async (TransportContainerAdminService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListActiveAsync(ct)))
            .WithTags("Issuing")
            .RequireAuthenticatedUser();
        var containers = app.MapGroup("/api/admin/transport-containers").WithTags("Admin: Transport Containers").RequireAuthenticatedUser();
        containers.MapGet("", async (TransportContainerAdminService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);
        containers.MapPost("", async (SaveTransportContainerRequest req, TransportContainerAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);
        containers.MapPut("/{id:long}", async (long id, SaveTransportContainerRequest req, TransportContainerAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);
        containers.MapPost("/{id:long}/activate", async (long id, TransportContainerAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, true, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
        containers.MapPost("/{id:long}/deactivate", async (long id, TransportContainerAdminService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, false, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        app.MapPost("/api/mtp/{id:long}/close", async (long id, CloseMtpRequest request, MtpActivationService svc, CancellationToken ct) =>
                EndpointResults.FromEvaluation(await svc.CloseAsync(id, request, ct), d => d))
            .WithTags("Issuing")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.IssueEmergencyRelease);

        issues.MapGet("/pending-retrospective-crossmatch", async (IssuingService service, CancellationToken ct) =>
            Results.Ok(await service.ListPendingRetrospectiveCrossmatchesAsync(ct)))
            .RequirePermission(PermissionCodes.IssueCreate);

        issues.MapGet("/in-transit", async (IssuingService service, CancellationToken ct) =>
            Results.Ok(await service.ListInTransitAsync(ct)))
            .RequirePermission(PermissionCodes.IssueCreate);

        issues.MapGet("/outstanding", async (IssuingService service, CancellationToken ct) =>
            Results.Ok(await service.ListOutstandingIssuedAsync(ct)))
            .RequireAuthenticatedUser();

        issues.MapGet("/overdue-disposition", async (FinalDispositionBoardService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)))
            .RequireAuthenticatedUser();

        issues.MapGet("/pending-second-abo", async (IssuingService service, CancellationToken ct) =>
            Results.Ok(await service.ListPendingSecondAboAsync(ct)))
            .RequirePermission(PermissionCodes.IssueCreate);

        issues.MapGet("/ready-to-issue", async (IssuingService service, CancellationToken ct) =>
            Results.Ok(await service.ListReadyToIssueAllocationsAsync(ct)))
            .RequirePermission(PermissionCodes.IssueCreate);

        issues.MapGet("/{id:long}", async (long id, IssuingService service, CancellationToken ct) =>
        {
            var issue = await service.GetAsync(id, ct);
            return issue is null ? Results.NotFound() : Results.Ok(IssueDto.From(issue));
        }).RequireAuthenticatedUser();

        // Same access as reading by id; lets screens start from the unit number on the bag.
        issues.MapGet("/latest-for-unit/{unitNumber}", async (string unitNumber, IssuingService service, CancellationToken ct) =>
        {
            var issue = await service.GetLatestForUnitNumberAsync(unitNumber, ct);
            return issue is null
                ? Results.NotFound(new { error = $"No issue found for unit {unitNumber.Trim()}." })
                : Results.Ok(IssueDto.From(issue));
        }).RequireAuthenticatedUser();

        issues.MapPost("/{id:long}/ward-receipt", async (long id, WardReceiptRequest request, IssuingService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.RecordWardReceiptAsync(id, request, ct), i => IssueDto.From(i)))
            .RequirePermission(PermissionCodes.TransfusionDocument);

        issues.MapPost("/{id:long}/return", async (
            long id,
            ReturnUnitRequest request,
            IssuingService service,
            BillingService billing,
            CancellationToken ct) =>
        {
            var result = await service.ReturnUnitAsync(id, request, ct);
            result = await WithReturnChargesAsync(billing, result, request.Reason, ct);
            return EndpointResults.FromEvaluation(result, r => ReturnDto.From(r));
        }).RequirePermission(PermissionCodes.IssueReturn);

        // Bedside start, then completion. The service accepts transfusion.start / transfusion.complete or transfusion.document.
        issues.MapPost("/{id:long}/transfusion/start", async (
            long id,
            StartTransfusionRequest request,
            IssuingService service,
            CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.StartTransfusionAsync(id, request, ct), t => TransfusionEventDto.From(t)));

        issues.MapPost("/{id:long}/transfusion/complete", async (
            long id,
            CompleteTransfusionRequest request,
            IssuingService service,
            BillingService billing,
            CancellationToken ct) =>
        {
            var result = await service.CompleteTransfusionAsync(id, request, ct);
            if (result.Succeeded)
            {
                await billing.CaptureForTransfusionAsync(id, result.Value!.FinalDisposition, ct);
            }

            return EndpointResults.FromEvaluation(result, t => TransfusionEventDto.From(t));
        });

        issues.MapPost("/{id:long}/transfusion", async (
            long id,
            DocumentTransfusionRequest request,
            IssuingService service,
            BillingService billing,
            CancellationToken ct) =>
        {
            var result = await service.DocumentTransfusionAsync(id, request, ct);
            if (result.Succeeded)
            {
                await billing.CaptureForTransfusionAsync(id, result.Value!.FinalDisposition, ct);
            }

            return EndpointResults.FromEvaluation(result, t => TransfusionEventDto.From(t));
        }).RequirePermission(PermissionCodes.TransfusionDocument);
    }

    internal static async Task<EvaluationResult<Return>> WithReturnChargesAsync(
        BillingService billing,
        EvaluationResult<Return> result,
        string reason,
        CancellationToken ct)
    {
        if (!result.Succeeded || result.Value is null)
        {
            return result;
        }

        var charges = await billing.CancelOpenIssueChargesForReturnAsync(result.Value.IssueId, reason, ct);
        if (charges.Succeeded && !charges.HasWarnings)
        {
            return result;
        }

        var messages = charges.Succeeded
            ? charges.Warnings
            : [RuleResult.Warning(
                "BILL-RETURN-OPEN",
                charges.Error ?? "Issue charges could not be cancelled.")];
        return EvaluationResult<Return>.Ok(result.Value, new RuleEvaluation(messages));
    }

    private static bool IsOverrideAttempt(IssueUnitRequest request) =>
        !string.IsNullOrWhiteSpace(request.OverrideReason)
        || !string.IsNullOrWhiteSpace(request.AuthorizedBy)
        || request.IssueType == Domain.Enums.IssueType.EmergencyRelease
        || request.IssueType == Domain.Enums.IssueType.MassiveTransfusion;

    private static async Task<bool> TryValidateOverrideSignatureAsync(
        HttpContext http, ISignatureService signatures, CancellationToken ct)
    {
        var header = http.Request.Headers[OverrideSignatureHeader].ToString();
        return long.TryParse(header, out var signatureId)
            && await signatures.IsValidForCurrentUserAsync(signatureId, OverrideSignatureAction, ct);
    }
}
