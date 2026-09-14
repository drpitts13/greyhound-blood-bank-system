using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Auth;

internal static class AuthHttpContext
{
    public const string IdentityKey = "BloodBank.Auth.Identity";
    public const string SessionIdKey = "BloodBank.Auth.SessionId";
    public const string BearerTokenKey = "BloodBank.Auth.BearerToken";

    public static RequestIdentity GetIdentity(HttpContext http) =>
        http.Items.TryGetValue(IdentityKey, out var value) && value is RequestIdentity identity
            ? identity
            : RequestIdentity.Anonymous();
}
