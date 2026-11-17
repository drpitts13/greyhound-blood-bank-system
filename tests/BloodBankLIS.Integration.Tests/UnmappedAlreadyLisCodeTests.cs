using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class UnmappedAlreadyLisCodeTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // TEST-BB-1226: an open unmapped inbound order code that is now the LIS's own test code is marked as needing no
    // translation; a code the LIS does not know is not (end-user testing pass 204).
    [Fact]
    public async Task OpenOrderCodeThatIsAnLisCode_IsMarked()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var code = $"XQ{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        c.TestDefinitions.Add(new TestDefinition { Code = code, Name = "Pass 204 test", IsActive = true, IsDraft = false });
        c.InterfaceUnmappedValues.AddRange(
            new InterfaceUnmappedValue { DataItemKey = "Order.TestCode", Value = code.ToLowerInvariant(), Direction = InterfaceTranslationDirection.Inbound, FirstSeenUtc = now, LastSeenUtc = now, Count = 1 },
            new InterfaceUnmappedValue { DataItemKey = "Order.TestCode", Value = "LAB-NOT-LIS-204", Direction = InterfaceTranslationDirection.Inbound, FirstSeenUtc = now, LastSeenUtc = now, Count = 1 });
        await c.SaveChangesAsync();

        var service = new InterfaceUnmappedValueService(new EfRepository<InterfaceUnmappedValue>(c), c, _factory.Clock,
            valueSets: new InterfaceValueSets(new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), new EfRepository<ProductType>(c)));
        var rows = await service.ListAsync(openOnly: true);

        Assert.True(rows.Single(r => string.Equals(r.Value, code, StringComparison.OrdinalIgnoreCase)).AlreadyLisCode);
        Assert.False(rows.Single(r => r.Value == "LAB-NOT-LIS-204").AlreadyLisCode);
    }
}
