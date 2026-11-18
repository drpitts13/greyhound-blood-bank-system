namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Who authorized an emergency or massive-transfusion issue, as the issue worklists and product history show it.
/// End-user testing pass 250: In transit read "Authorized by: —" for four units issued under MTP #1, while the MTP panel
/// above them named the physician who called the protocol. An issue's override authorizer is used when one was recorded;
/// otherwise a massive-transfusion issue names the physician who called its protocol, marked as such.
/// </summary>
public static class IssueAuthorizerText
{
    public static string? For(string? overrideAuthorizer, long? mtpActivationId, string? mtpRequestedBy)
    {
        if (!string.IsNullOrWhiteSpace(overrideAuthorizer))
        {
            return overrideAuthorizer.Trim();
        }

        return mtpActivationId is long id && !string.IsNullOrWhiteSpace(mtpRequestedBy)
            ? $"{mtpRequestedBy.Trim()} (called MTP #{id})"
            : null;
    }
}
