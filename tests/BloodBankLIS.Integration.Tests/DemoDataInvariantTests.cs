using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-345: seeded demo data only shows states the system itself produces.
public class DemoDataInvariantTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SeededUnitsIssuesAndRecords_AreConsistent()
    {
        await using (var db = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var v = _factory.Create();
        var units = await v.BloodUnits.AsNoTracking().ToDictionaryAsync(u => u.Id);
        var issues = await v.Issues.AsNoTracking().ToListAsync();
        var transfusions = await v.TransfusionEvents.AsNoTracking().ToListAsync();
        var returns = await v.Returns.AsNoTracking().ToListAsync();
        var allocations = await v.Allocations.AsNoTracking().ToListAsync();
        var problems = new List<string>();

        foreach (var unit in units.Values.Where(u => u.Status is UnitStatus.Issued or UnitStatus.TransfusionStarted))
        {
            if (!issues.Any(i => i.BloodProductId == unit.Id && i.Status == IssueStatus.Issued))
            {
                problems.Add($"Unit {unit.UnitNumber} is {unit.Status} with no open issue.");
            }
        }

        foreach (var issue in issues)
        {
            var unit = units[issue.BloodProductId];
            var records = transfusions.Where(t => t.IssueId == issue.Id).ToList();
            switch (issue.Status)
            {
                case IssueStatus.Issued when unit.Status is not (UnitStatus.Issued or UnitStatus.TransfusionStarted):
                    problems.Add($"Issue {issue.Id} is open but unit {unit.UnitNumber} is {unit.Status}.");
                    break;
                case IssueStatus.Transfused when records.Count == 0:
                    problems.Add($"Issue {issue.Id} is Transfused with no transfusion record.");
                    break;
                case IssueStatus.Returned when !returns.Any(r => r.IssueId == issue.Id):
                    problems.Add($"Issue {issue.Id} is Returned with no return record.");
                    break;
            }

            if (issues.Count(i => i.BloodProductId == issue.BloodProductId && i.Status == IssueStatus.Issued) > 1)
            {
                problems.Add($"Unit {unit.UnitNumber} has more than one open issue.");
            }
        }

        foreach (var unit in units.Values.Where(u => u.Status == UnitStatus.Discarded && string.IsNullOrWhiteSpace(u.DiscardReasonCode)))
        {
            problems.Add($"Unit {unit.UnitNumber} is Discarded without a discard reason code.");
        }

        foreach (var allocation in allocations.Where(a => a.Status == AllocationStatus.Reserved))
        {
            var unit = units[allocation.BloodProductId];
            if (unit.Status is UnitStatus.Issued or UnitStatus.TransfusionStarted or UnitStatus.Transfused
                or UnitStatus.TransfusionStopped or UnitStatus.Discarded or UnitStatus.Expired)
            {
                problems.Add($"Reservation {allocation.Id} is still Reserved but unit {unit.UnitNumber} is {unit.Status}.");
            }
        }

        foreach (var specimen in await v.Specimens.AsNoTracking().Where(s => s.Status == SpecimenStatus.Rejected).ToListAsync())
        {
            if (string.IsNullOrWhiteSpace(specimen.RejectionReasonCode))
            {
                problems.Add($"Specimen {specimen.AccessionNumber} is Rejected without a rejection reason code.");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }
}
