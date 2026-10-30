using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Two users saving the same unique value must surface as a recognized conflict
/// (mapped to 409 by the API), not an unclassified database error.
/// </summary>
public sealed class DatabaseConflictTests
{
    [Fact]
    public async Task Duplicate_unique_value_is_classified_with_table()
    {
        using var factory = new SqliteContextFactory();

        await using (var first = factory.Create())
        {
            first.Add(new BloodSupplier { Code = "SUP-DUP", Name = "First", Registration = "R1" });
            await first.SaveChangesAsync();
        }

        await using var second = factory.Create();
        second.Add(new BloodSupplier { Code = "SUP-DUP", Name = "Second", Registration = "R2" });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        Assert.True(DatabaseConflict.TryGetUniqueViolationTable(ex, out var table));
        Assert.Equal("BloodSuppliers", table);
        Assert.True(DatabaseConflict.TryDescribe(ex, out var message));
        Assert.Contains("Refresh", message);
    }

    [Fact]
    public void Concurrency_exception_is_classified()
    {
        var ex = new DbUpdateConcurrencyException("stale", new List<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        Assert.True(DatabaseConflict.TryDescribe(ex, out var message));
        Assert.Contains("changed by another user", message);
    }

    [Fact]
    public void Unrelated_exception_is_not_a_conflict()
    {
        Assert.False(DatabaseConflict.TryDescribe(new InvalidOperationException("boom"), out _));
    }
}
