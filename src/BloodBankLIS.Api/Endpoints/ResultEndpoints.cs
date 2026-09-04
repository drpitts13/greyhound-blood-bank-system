using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class ResultEndpoints
{
    public const string OverrideSignatureHeader = IssuingEndpoints.OverrideSignatureHeader;
    private const string OverrideSignatureAction = "ResultOverride";

    public static void MapResultEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/results").WithTags("Results").RequireAuthenticatedUser();

        group.MapGet("/{id:long}", async (long id, ResultService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(TestResultDto.From(result));
        });

        group.MapPost("/", async (EnterResultRequest request, ResultService service, CancellationToken ct) =>
            EndpointResults.Created(await service.EnterResultAsync(request, ct),
                r => ($"/api/results/{r.Id}", (object)TestResultDto.From(r))))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapPost("/abo-rh", async (EnterAboRhRequest request, ResultService service, CancellationToken ct) =>
            EndpointResults.Created(await service.EnterAboRhAsync(request, ct),
                r => ($"/api/results/{r.Id}", (object)TestResultDto.From(r))))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapPost("/save", async (
            SaveTestResultRequest request,
            ResultService service,
            BillingService billing,
            ISignatureService signatures,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (request.MarkComplete && IsDeltaOverrideAttempt(request.OverrideReason, request.AuthorizedBy, request.HistoryResolution, request.SignatureId))
            {
                var signatureValid = await TryValidateOverrideSignatureAsync(http, signatures, ct);
                if (!signatureValid)
                {
                    return Results.Problem(
                        title: "Electronic signature required",
                        detail: $"Supply a valid '{OverrideSignatureAction}' signature id via the '{OverrideSignatureHeader}' header.",
                        statusCode: StatusCodes.Status403Forbidden);
                }
            }

            var result = await service.SaveTestResultAsync(request, ct);
            if (result.Succeeded && result.Value!.Status == ResultStatus.Verified)
            {
                await billing.CaptureForResultAsync(result.Value.Id, ct);
            }

            return EndpointResults.FromEvaluation(result, r => TestResultDto.From(r));
        }).RequirePermission(PermissionCodes.ResultEnter);

        // Verifying a result is a billing trigger: on success, capture charges after the
        // clinical action has committed (a blocked/failed verify never bills).
        group.MapPost("/{id:long}/verify", async (
            long id,
            VerifyResultRequest? request,
            ResultService service,
            BillingService billing,
            ISignatureService signatures,
            HttpContext http,
            CancellationToken ct) =>
        {
            request ??= new VerifyResultRequest();
            if (IsDeltaOverrideAttempt(request.OverrideReason, request.AuthorizedBy, request.HistoryResolution, request.SignatureId))
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

            var result = await service.VerifyResultAsync(id, request, ct);
            if (result.Succeeded)
            {
                await billing.CaptureForResultAsync(result.Value!.Id, ct);
            }

            return EndpointResults.FromEvaluation(result, r => TestResultDto.From(r));
        }).RequirePermission(PermissionCodes.ResultVerify);

        group.MapPost("/{id:long}/correct", async (long id, CorrectResultRequest request, ResultService service, CancellationToken ct) =>
            EndpointResults.From(await service.CorrectResultAsync(id, request.NewValue, request.Reason, ct), r => TestResultDto.From(r)))
            .RequirePermission(PermissionCodes.ResultCorrect);

        app.MapGet("/api/specimens/{specimenId:long}/results", async (long specimenId, ResultService service, CancellationToken ct) =>
        {
            var results = await service.GetBySpecimenAsync(specimenId, ct);
            return Results.Ok(results.Select(TestResultDto.From));
        }).WithTags("Results");
    }

    private static bool IsDeltaOverrideAttempt(
        string? overrideReason,
        string? authorizedBy,
        Domain.Enums.AboRhHistoryResolution? historyResolution,
        long? signatureId) =>
        !string.IsNullOrWhiteSpace(overrideReason)
        || !string.IsNullOrWhiteSpace(authorizedBy)
        || historyResolution is not null
        || signatureId is not null;

    private static async Task<bool> TryValidateOverrideSignatureAsync(
        HttpContext http, ISignatureService signatures, CancellationToken ct)
    {
        var header = http.Request.Headers[OverrideSignatureHeader].ToString();
        return long.TryParse(header, out var signatureId)
            && await signatures.IsValidForCurrentUserAsync(signatureId, OverrideSignatureAction, ct);
    }
}
