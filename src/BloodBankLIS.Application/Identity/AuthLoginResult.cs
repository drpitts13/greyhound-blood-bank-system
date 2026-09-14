namespace BloodBankLIS.Application.Identity;

public sealed record AuthLoginResult(
    string SessionToken,
    string UserName,
    string DisplayName,
    int SecurityLevel,
    IReadOnlyList<string> Permissions);
