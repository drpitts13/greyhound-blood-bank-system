using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Identity;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class MfaEndpoints
{
    public static void MapMfaEndpoints(this WebApplication app)
    {
        var account = app.MapGroup("/api/account/mfa").WithTags("Identity").RequireAuthenticatedUser();

        account.MapGet("/", async (MfaService service, CancellationToken ct) => ToResult(await service.GetStatusAsync(ct)));

        account.MapPost("/begin", async (MfaService service, CancellationToken ct) => ToResult(await service.BeginEnrollmentAsync(ct)));

        account.MapPost("/confirm", async (MfaCodeRequest request, MfaService service, CancellationToken ct) =>
            ToResult(await service.ConfirmEnrollmentAsync(request, ct)));

        account.MapPost("/disable", async (MfaCodeRequest request, MfaService service, CancellationToken ct) =>
            ToResult(await service.DisableAsync(request, ct)));

        app.MapPost("/api/admin/users/{id:long}/mfa/reset", async (long id, MfaService service, CancellationToken ct) =>
                ToResult(await service.ResetAsync(id, ct)))
            .WithTags("Admin: Users")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.AdminUsersManage);
    }

    private static IResult ToResult<T>(OperationResult<T> result) =>
        result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
}
