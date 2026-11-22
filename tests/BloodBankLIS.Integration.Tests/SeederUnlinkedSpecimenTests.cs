using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// The demo seeder runs at every start. Once a demo tube expired, the expired-specimen sweep unlinked it from its order,
/// and the seeder (whose existence check could not see unlinked rows) added the pair again: the unique (order, specimen)
/// index failed and the API would not start (found Cycle 600).
/// </summary>
public class SeederUnlinkedSpecimenTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SeederUnlinkedSpecimenTests(SqliteContextFactory factory) => _factory = factory;

    // TEST-BB-710: seeding again after the sweep unlinked the demo order-specimen links succeeds and adds no duplicate.
    [Fact]
    public async Task Reseed_AfterLinksWereUnlinked_DoesNotFail()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        int links;
        await using (var context = _factory.Create())
        {
            var all = await context.OrderSpecimens.ToListAsync();
            Assert.NotEmpty(all);
            foreach (var link in all)
            {
                link.UnlinkedUtc = DateTime.UtcNow;
                link.UnlinkReason = "Specimen expired before testing.";
            }

            await context.SaveChangesAsync();
            links = await context.OrderSpecimens.IgnoreQueryFilters().CountAsync();
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        Assert.Equal(links, await verify.OrderSpecimens.IgnoreQueryFilters().CountAsync());
    }
}
