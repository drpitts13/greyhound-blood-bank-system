using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

/// <summary>End-user testing pass 97: finding instrument messages, older ones included.</summary>
public class InstrumentMessageSearchTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InstrumentMessageSearchTests(SqliteContextFactory factory) => _factory = factory;

    // TEST-BB-1111: text matches the specimen id, the patient id, or anything in the message, ignoring case; the
    // interface narrows it; no text finds them all.
    [Fact]
    public async Task Filter_FindsBySpecimenPatientOrMessageText()
    {
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var c = _factory.Create();
        var iface = new InstrumentInterface { Name = $"Search {key}" };
        c.InstrumentInterfaces.Add(iface);
        await c.SaveChangesAsync();
        InstrumentMessageLog Log(string? specimen, string? patient, string raw) => new()
        {
            InterfaceId = iface.Id, Direction = Hl7Direction.Inbound, RawMessage = raw, SpecimenId = specimen, PatientId = patient,
            Status = InstrumentMessageStatus.Processed, ReceivedUtc = _factory.Clock.UtcNow
        };
        c.InstrumentMessages.AddRange(
            Log($"ACC-{key}-1", $"MRN-{key}", $"H|\\^&|||Echo\rO|1|ACC-{key}-1||^^^Group"),
            Log($"ACC-{key}-2", null, $"H|\\^&|||Echo\rO|1|ACC-{key}-2||^^^Screen"),
            Log(null, null, $"H|\\^&|||Echo\rQ|1|^QRY-{key}"));
        await c.SaveChangesAsync();
        var repo = new EfRepository<InstrumentMessageLog>(c);

        async Task<int> Count(string? text) => (await repo.ListAsync(InstrumentMessageSearch.Filter(iface.Id, text))).Count;

        Assert.Equal(1, await Count($"acc-{key.ToLowerInvariant()}-1"));
        Assert.Equal(1, await Count($"mrn-{key}"));
        Assert.Equal(1, await Count("^^^screen"));
        Assert.Equal(1, await Count($"qry-{key}"));
        Assert.Equal(2, await Count($"ACC-{key}"));
        Assert.Equal(3, await Count(" "));
        Assert.Empty(await repo.ListAsync(InstrumentMessageSearch.Filter(iface.Id + 100000, $"ACC-{key}")));
    }
}
