namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Decides the authenticated operator from a resolved session, optional
/// test-only legacy header, or Development DevMode. Never treats a spoofable
/// user-name header as identity when the legacy flag is off.
/// </summary>
public static class RequestIdentityResolver
{
    public const string BearerScheme = "Bearer";

    public static RequestIdentity Resolve(
        AuthSessionPrincipal? session,
        string? legacyUserHeader,
        string? legacyWorkstationHeader,
        bool allowLegacyIdentityHeader,
        bool devModeEnabled,
        string? devModeUserName)
    {
        if (session is not null)
        {
            return RequestIdentity.Authenticated(session.UserName, session.Workstation);
        }

        if (allowLegacyIdentityHeader && !string.IsNullOrWhiteSpace(legacyUserHeader))
        {
            return RequestIdentity.Authenticated(
                legacyUserHeader.Trim(),
                string.IsNullOrWhiteSpace(legacyWorkstationHeader) ? null : legacyWorkstationHeader.Trim());
        }

        if (devModeEnabled && !string.IsNullOrWhiteSpace(devModeUserName))
        {
            return RequestIdentity.Authenticated(devModeUserName, null);
        }

        return RequestIdentity.Anonymous();
    }

    public static string? ReadBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        var value = authorizationHeader.Trim();
        if (!value.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = value[BearerScheme.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}

public sealed record AuthSessionPrincipal(long SessionId, long UserId, string UserName, string? Workstation);

public readonly record struct RequestIdentity(bool IsAuthenticated, string UserName, string? Workstation)
{
    public static RequestIdentity Anonymous() => new(false, "system", null);

    public static RequestIdentity Authenticated(string userName, string? workstation) =>
        new(true, userName, workstation);
}
