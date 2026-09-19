using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class LastInteractiveClientGateTests
{
    [Fact]
    public void StartupWithNoClients_DoesNotRequestStop()
    {
        var gate = new LastInteractiveClientGate();
        Assert.False(gate.Disconnect("never-connected"));
        Assert.Equal(0, gate.ConnectedCount);
    }

    [Fact]
    public void LastClientLeaving_RequestsStop()
    {
        var gate = new LastInteractiveClientGate();
        gate.Connect("a");
        Assert.True(gate.Disconnect("a"));
        Assert.Equal(0, gate.ConnectedCount);
    }

    [Fact]
    public void OtherClientStillConnected_DoesNotRequestStop()
    {
        var gate = new LastInteractiveClientGate();
        gate.Connect("a");
        gate.Connect("b");
        Assert.False(gate.Disconnect("a"));
        Assert.Equal(1, gate.ConnectedCount);
        Assert.True(gate.Disconnect("b"));
    }

    [Fact]
    public void ReconnectAfterLastLeave_KeepsPresence()
    {
        var gate = new LastInteractiveClientGate();
        gate.Connect("a");
        Assert.True(gate.Disconnect("a"));
        gate.Connect("b");
        Assert.Equal(1, gate.ConnectedCount);
        Assert.False(gate.Disconnect("missing"));
    }
}
