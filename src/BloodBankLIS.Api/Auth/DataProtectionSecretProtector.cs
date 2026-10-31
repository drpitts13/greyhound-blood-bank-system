using BloodBankLIS.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace BloodBankLIS.Api.Auth;

/// <summary>Protects authenticator keys with ASP.NET Core Data Protection (purpose-scoped).</summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    public const string Purpose = "BloodBankLIS.Totp.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
