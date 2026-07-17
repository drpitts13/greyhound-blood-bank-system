using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Issuing;
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
            // Issuing a unit is a billing trigger; capture only on a committed issue.
            if (result.Succeeded)
            {
                await billing.CaptureForIssueAsync(result.Value!.Id, ct);
            }

            return EndpointResults.CreatedEvaluation(result, i => ($"/api/issues/{i.Id}", (object)IssueDto.From(i)));
        }).RequirePermission(PermissionCodes.IssueCreate);

        issues.MapPost("/{id:long}/return", async (long id, ReturnUnitRequest request, IssuingService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.ReturnUnitAsync(id, request, ct), r => ReturnDto.From(r)))
            .RequirePermission(PermissionCodes.IssueReturn);

        issues.MapPost("/{id:long}/transfusion", async (long id, DocumentTransfusionRequest request, IssuingService service, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await service.DocumentTransfusionAsync(id, request, ct), t => TransfusionEventDto.From(t)))
            .RequirePermission(PermissionCodes.TransfusionDocument);
    }

    private static bool IsOverrideAttempt(IssueUnitRequest request) =>
        !string.IsNullOrWhiteSpace(request.OverrideReason)
        || !string.IsNullOrWhiteSpace(request.AuthorizedBy)
        || request.UnresolvedAboRhDiscrepancy
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
