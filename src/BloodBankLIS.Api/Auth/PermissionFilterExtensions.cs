using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Auth;

/// <summary>
/// Endpoint-filter authorization. Authorization is enforced at the API boundary (not in
/// the UI), so every HTTP caller is subject to the same default-deny permission check
/// (see docs/architecture.md 4.2). HL7/MLLP is a separately trusted interface with its
/// own endpoint configuration and is not exposed through these HTTP routes.
/// </summary>
public static class PermissionFilterExtensions
{
    /// <summary>Requires only that an identity was supplied (no specific permission).</summary>
    public static TBuilder RequireAuthenticatedUser<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var current = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
            var isAuthenticated = current is HttpCurrentUser shim
                ? shim.IsAuthenticated
                : !string.IsNullOrWhiteSpace(current.UserName) && current.UserName != "system";

            return isAuthenticated
                ? await next(context)
                : Results.Problem(
                    title: "Authentication required",
                    detail: $"Supply an identity via the '{HttpCurrentUser.UserHeader}' header.",
                    statusCode: StatusCodes.Status401Unauthorized);
        });

        return builder;
    }

    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionCode)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var current = http.RequestServices.GetRequiredService<ICurrentUser>();
            var evaluator = http.RequestServices.GetRequiredService<IPermissionEvaluator>();

            var isAuthenticated = current is HttpCurrentUser shim
                ? shim.IsAuthenticated
                : !string.IsNullOrWhiteSpace(current.UserName) && current.UserName != "system";

            var granted = isAuthenticated
                ? await evaluator.GetPermissionsAsync(current.UserName)
                : null;

            var decision = PermissionPolicy.Evaluate(isAuthenticated, granted, permissionCode);
            return decision switch
            {
                AccessDecision.Unauthenticated => Results.Problem(
                    title: "Authentication required",
                    detail: $"Supply an identity via the '{HttpCurrentUser.UserHeader}' header.",
                    statusCode: StatusCodes.Status401Unauthorized),
                AccessDecision.Forbidden => Results.Problem(
                    title: "Forbidden",
                    detail: $"The '{permissionCode}' permission is required for this action.",
                    statusCode: StatusCodes.Status403Forbidden),
                _ => await next(context)
            };
        });

        return builder;
    }
}
