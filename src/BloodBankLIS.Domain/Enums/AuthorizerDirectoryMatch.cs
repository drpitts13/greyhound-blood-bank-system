namespace BloodBankLIS.Domain.Enums;

/// <summary>How an override's typed authorizer matched the directory when the override was recorded.</summary>
public enum AuthorizerDirectoryMatch
{
    NotInDirectory = 0,
    OrderingProvider = 1,
    User = 2,
    Ambiguous = 3,

    /// <summary>An outside provider added to the directory for overrides (OCD-055).</summary>
    OutsideProvider = 4
}
