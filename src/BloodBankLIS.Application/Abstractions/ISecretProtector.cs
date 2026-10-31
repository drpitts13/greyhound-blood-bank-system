namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Reversible protection for secrets the application must read back, such as a user's
/// authenticator key. The API implements it with ASP.NET Core Data Protection, so a
/// copied database alone does not reveal the keys.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedText);
}
