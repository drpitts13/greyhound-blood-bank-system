using BloodBankLIS.Application.Billing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Outbound results and charges that have not gone out (Cycle 660): queued with no interface to take them, or retries
/// spent, they waited silently; they never reached the error queue because nothing errored.
/// </summary>
public class Hl7OutboundWaitingTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private Hl7OutboundSender Sender(BloodBankDbContext c) =>
        new(new EfRepository<Hl7MessageLog>(c), new EfRepository<InterfaceEndpoint>(c), new EfRepository<InterfaceErrorQueueItem>(c), c, _factory.Clock);

    private Hl7MessageLog Outbound(string control, Hl7MessageStatus status, TimeSpan age, int retries = 0, string? detail = null, string type = "DFT^P03") => new()
    {
        Direction = Hl7Direction.Outbound, MessageType = type, MessageControlId = control, RawMessage = "MSH|^~\\&|",
        Status = status, ReceivedUtc = _factory.Clock.UtcNow - age, RetryCount = retries, ErrorDetail = detail
    };

    // TEST-BB-837: with no outbound interface, queued and failed messages are listed oldest first as waiting for one, and
    // stuck once past fifteen minutes; acknowledged messages and a charge withheld because it was cancelled are not.
    [Fact]
    public async Task NoInterface_MessagesWaitForOne()
    {
        await using var c = _factory.Create();
        c.Hl7Messages.AddRange(
            Outbound("OLD", Hl7MessageStatus.Received, TimeSpan.FromDays(3)),
            Outbound("NEW", Hl7MessageStatus.Received, TimeSpan.FromMinutes(2), type: "ORU^R01"),
            Outbound("SENT", Hl7MessageStatus.Acked, TimeSpan.FromDays(1)),
            Outbound("WITHHELD", Hl7MessageStatus.Received, TimeSpan.FromDays(1), detail: BillingService.UnsentDftWithheldDetail),
            new Hl7MessageLog { Direction = Hl7Direction.Inbound, MessageType = "ORM^O01", MessageControlId = "IN", RawMessage = "MSH|", Status = Hl7MessageStatus.Received, ReceivedUtc = _factory.Clock.UtcNow.AddDays(-2) });
        await c.SaveChangesAsync();

        var waiting = await Sender(c).ListWaitingAsync();
        Assert.Equal(["OLD", "NEW"], waiting.Select(w => w.ControlId));
        Assert.All(waiting, w => Assert.Equal(OutboundMessageWaitRule.NoInterface, w.Reason));
        Assert.Equal((true, false), (waiting[0].Stuck, waiting[1].Stuck));
        Assert.Equal("ORU^R01", waiting[1].MessageType);
    }

    // TEST-BB-838: with an interface, a message whose retries are spent says so, one whose last send failed says it is
    // retried, and a queued one says it goes within a minute; a disabled or incomplete interface counts as none.
    [Fact]
    public async Task WithAnInterface_SaysWhyEachWaits()
    {
        await using var c = _factory.Create();
        c.InterfaceEndpoints.Add(new InterfaceEndpoint
        {
            Name = "Billing out", Direction = Hl7Direction.Outbound, InterfaceType = InterfaceType.Results, MessageTypes = "DFT,ORU",
            Transport = InterfaceTransport.File, Path = Path.GetTempPath(), IsEnabled = true, MaxRetryCount = 3
        });
        c.Hl7Messages.AddRange(
            Outbound("SPENT", Hl7MessageStatus.Errored, TimeSpan.FromHours(2), retries: 3),
            Outbound("RETRY", Hl7MessageStatus.Errored, TimeSpan.FromMinutes(30), retries: 1),
            Outbound("QUEUED", Hl7MessageStatus.Received, TimeSpan.FromMinutes(1)));
        await c.SaveChangesAsync();

        var waiting = (await Sender(c).ListWaitingAsync()).ToDictionary(w => w.ControlId);
        Assert.Equal("Sending failed 3 times and retries are spent. Fix the interface, then Send.", waiting["SPENT"].Reason);
        Assert.Equal("The last send failed; it is retried (1 of 3 tries so far).", waiting["RETRY"].Reason);
        Assert.Equal("Queued; the next send attempt is within a minute.", waiting["QUEUED"].Reason);
        Assert.Equal((true, true, false), (waiting["SPENT"].Stuck, waiting["RETRY"].Stuck, waiting["QUEUED"].Stuck));

        var endpoint = c.InterfaceEndpoints.Single();
        endpoint.IsEnabled = false;
        await c.SaveChangesAsync();
        Assert.All(await Sender(c).ListWaitingAsync(), w => Assert.Equal(OutboundMessageWaitRule.NoInterface, w.Reason));
    }
}
