using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

/// <summary>End-user testing pass 96: finding messages in the HL7 log.</summary>
public class Hl7MessageSearchTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Hl7MessageSearchTests(SqliteContextFactory factory) => _factory = factory;

    // TEST-BB-1110: text matches the control id or anything in the message (MRN, name, order), ignoring case; type,
    // direction and status narrow it; the limit is 200 by default and at most 1000.
    [Fact]
    public async Task Filter_FindsByTextTypeDirectionAndStatus()
    {
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var c = _factory.Create();
        Hl7MessageLog Log(string type, string control, string raw, Hl7Direction dir, Hl7MessageStatus status) => new()
        {
            MessageType = type, MessageControlId = control, RawMessage = raw, Direction = dir, Status = status, ReceivedUtc = _factory.Clock.UtcNow
        };
        c.Hl7Messages.AddRange(
            Log("ADT", $"ADT-{key}", $"MSH|^~\\&|EHR|H|BB|L|20261115||ADT^A04|ADT-{key}|P|2.5\rPID|1||MRN-{key}^^^H^MR||Search^Wanda", Hl7Direction.Inbound, Hl7MessageStatus.Processed),
            Log("ORM", $"ORM-{key}", $"MSH|^~\\&|EHR|H|BB|L|20261115||ORM^O01|ORM-{key}|P|2.5\rPID|1||MRN-{key}^^^H^MR||Search^Wanda\rORC|NW|PL-{key}", Hl7Direction.Inbound, Hl7MessageStatus.Errored),
            Log("DFT", $"DFT-{key}", "MSH|^~\\&|BB|L|EHR|H|20261115||DFT^P03|x|P|2.5\rPID|1||OTHER^^^H^MR", Hl7Direction.Outbound, Hl7MessageStatus.Received));
        await c.SaveChangesAsync();
        var repo = new EfRepository<Hl7MessageLog>(c);

        async Task<List<string>> Find(string? text, string? type = null, Hl7Direction? dir = null, Hl7MessageStatus? status = null) =>
            (await repo.ListAsync(Hl7MessageSearch.Filter(text, type, dir, status))).Where(m => m.MessageControlId.EndsWith(key)).Select(m => m.MessageType).OrderBy(t => t).ToList();

        Assert.Equal(["ADT", "ORM"], await Find($"mrn-{key.ToLowerInvariant()}"));
        Assert.Equal(["ADT", "ORM"], await Find("search^wanda"));
        Assert.Equal(["ORM"], await Find($"pl-{key}"));
        Assert.Equal(["DFT"], await Find($"dft-{key}"));
        Assert.Equal(["ORM"], await Find(null, "orm"));
        Assert.Equal(["DFT"], await Find(null, dir: Hl7Direction.Outbound));
        Assert.Equal(["ORM"], await Find(null, status: Hl7MessageStatus.Errored));
        Assert.Equal(["ADT", "DFT", "ORM"], await Find(" "));
        Assert.Equal((200, 1000, 50), (Hl7MessageSearch.Take(null), Hl7MessageSearch.Take(5000), Hl7MessageSearch.Take(50)));
    }
}
