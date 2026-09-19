using Microsoft.AspNetCore.Components.Server.Circuits;

namespace BloodBankLIS.Web.Hosting;

/// <summary>
/// Forwards Blazor circuit connect/disconnect to <see cref="DesktopHostLifetime"/>.
/// </summary>
public sealed class DesktopShutdownCircuitHandler : CircuitHandler
{
    private readonly DesktopHostLifetime _lifetime;

    public DesktopShutdownCircuitHandler(DesktopHostLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _lifetime.ClientConnected(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _lifetime.ClientDisconnected(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _lifetime.ClientDisconnected(circuit.Id);
        return Task.CompletedTask;
    }
}
