namespace BloodBankLIS.Web.Services;

/// <summary>
/// Attaches the current operator's identity headers to every outbound API request.
/// Scoped to the circuit so it reads the same <see cref="UserSession"/> the UI shows.
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
        if (_session.IsSignedIn)
        {
            request.Headers.Remove("X-User");
            request.Headers.Add("X-User", _session.UserName);
            request.Headers.Remove("X-Workstation");
            request.Headers.Add("X-Workstation", _session.Workstation);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
