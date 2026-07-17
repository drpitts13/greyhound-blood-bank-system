using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Electronic-signature capture. A signature is an append-only attestation bound to the
/// current user; the returned id is supplied (e.g. via the <c>X-Esignature-Id</c> header)
/// when performing a dangerous/override action so the attestation is preserved alongside
/// the audit trail (see docs/safety-rules.md).
/// </summary>
public static class SignatureEndpoints
{
    public sealed record RecordSignatureRequest(
        string Action,
        string MeaningOfSignature,
        string? ContextType = null,
        long? ContextId = null);

    public static void MapSignatureEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/signatures").WithTags("Signatures");

        group.MapPost("/", async (RecordSignatureRequest request, ISignatureService service, CancellationToken ct) =>
        {
            var result = await service.RecordAsync(
                request.Action, request.MeaningOfSignature, request.ContextType, request.ContextId, ct);

            return result.Succeeded
                ? Results.Created($"/api/signatures/{result.Value}", new { id = result.Value })
                : Results.BadRequest(new { error = result.Error });
        }).RequireAuthenticatedUser();
    }
}
