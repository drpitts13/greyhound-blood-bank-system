using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-344: seeded demo transfusions are consistent with their issue and unit.
public class DemoTransfusionConsistencyTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task EveryTransfusionOnAnIssuedIssue_IsARunningTransfusion()
    {
        await using (var db = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var verify = _factory.Create();
        var open = await (
            from t in verify.TransfusionEvents
            join i in verify.Issues on t.IssueId equals i.Id
            join u in verify.BloodUnits on t.BloodProductId equals u.Id
            where i.Status == IssueStatus.Issued
            select new { t.StopUtc, t.VolumeTransfused, UnitStatus = u.Status, u.UnitNumber }).ToListAsync();

        Assert.NotEmpty(open);
        Assert.All(open, row =>
        {
            Assert.Null(row.StopUtc);
            Assert.Null(row.VolumeTransfused);
            Assert.Equal(UnitStatus.TransfusionStarted, row.UnitStatus);
        });
        Assert.Contains(open, row => row.UnitNumber == "W0001230000099");
    }
}
