using System.Diagnostics;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Hosting;

/// <summary>
/// Stops the Web host after the last desktop UI session leaves (or on an
/// explicit Exit) and stops the background API process with it.
/// </summary>
public sealed class DesktopHostLifetime : IDisposable
{
    private readonly DesktopHostOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DesktopHostLifetime> _logger;
    private readonly LastInteractiveClientGate _clients = new();
    private readonly object _sync = new();
    private CancellationTokenSource? _pending;

    public DesktopHostLifetime(
        DesktopHostOptions options,
        IHostApplicationLifetime lifetime,
        ILogger<DesktopHostLifetime> logger)
    {
        _options = options;
        _lifetime = lifetime;
        _logger = logger;
        if (options.ShutdownOnExit)
        {
            lifetime.ApplicationStopping.Register(StopBackgroundApi);
        }
    }

    public bool ShutdownOnExit => _options.ShutdownOnExit;

    public void ClientConnected(string circuitId)
    {
        if (!ShutdownOnExit)
        {
            return;
        }

        lock (_sync)
        {
            _clients.Connect(circuitId);
            CancelPending();
        }
    }

    public void ClientDisconnected(string circuitId)
    {
        if (!ShutdownOnExit)
        {
            return;
        }

        lock (_sync)
        {
            if (!_clients.Disconnect(circuitId))
            {
                return;
            }

            CancelPending();
            _pending = new CancellationTokenSource();
            var token = _pending.Token;
            _ = StopAfterIdleAsync(token);
        }
    }

    public void RequestImmediateStop()
    {
        if (!ShutdownOnExit)
        {
            return;
        }

        lock (_sync)
        {
            CancelPending();
        }

        _logger.LogInformation("Desktop host stop requested.");
        _lifetime.StopApplication();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            CancelPending();
        }
    }

    private async Task StopAfterIdleAsync(CancellationToken token)
    {
        var idle = TimeSpan.FromSeconds(Math.Max(1, _options.IdleExitSeconds));
        try
        {
            await Task.Delay(idle, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_clients.ConnectedCount > 0)
        {
            return;
        }

        _logger.LogInformation("Last UI session left; stopping the desktop host.");
        _lifetime.StopApplication();
    }

    private void CancelPending()
    {
        var pending = _pending;
        _pending = null;
        if (pending is null)
        {
            return;
        }

        try
        {
            pending.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        pending.Dispose();
    }

    private void StopBackgroundApi()
    {
        foreach (var process in Process.GetProcessesByName("BloodBankLIS.Api"))
        {
            try
            {
                _logger.LogInformation("Stopping background API process {ProcessId}.", process.Id);
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Background API process {ProcessId} was already exiting.", process.Id);
            }
        }
    }
}
