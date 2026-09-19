namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Tracks interactive UI sessions (Blazor circuits) so a desktop host can stop
/// when the last client leaves. Startup with zero clients does not request stop.
/// </summary>
public sealed class LastInteractiveClientGate
{
    private readonly HashSet<string> _clients = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _seenAny;

    public int ConnectedCount
    {
        get
        {
            lock (_gate)
            {
                return _clients.Count;
            }
        }
    }

    public void Connect(string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        lock (_gate)
        {
            _clients.Add(clientId);
            _seenAny = true;
        }
    }

    /// <summary>
    /// Returns true when the last interactive client has left after at least one
    /// client had connected.
    /// </summary>
    public bool Disconnect(string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        lock (_gate)
        {
            _clients.Remove(clientId);
            return _seenAny && _clients.Count == 0;
        }
    }
}
