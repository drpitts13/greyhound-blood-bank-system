namespace BloodBankLIS.Web.Services;

/// <summary>
/// Attaches the circuit session token to every outbound API request.
/// Does not send a self-asserted user-name header.
/// </summary>
public sealed class IdentityHeaderHandler : DelegatingHandler
{
    private readonly UserSession _session;

    public IdentityHeaderHandler(UserSession session)
    {
        _session = session;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_session.SessionToken))
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_session.SessionToken}");
        }

        if (!string.IsNullOrWhiteSpace(_session.Workstation))
        {
            request.Headers.Remove("X-Workstation");
            request.Headers.Add("X-Workstation", _session.Workstation);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
