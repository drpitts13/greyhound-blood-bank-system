using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-337: every new override records how its authorizer matched the directory, at save time.
public class OverrideAuthorizerMatchTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task SeedDirectoryAsync()
    {
        await using var db = _factory.Create();
        db.OrderingProviders.AddRange(
            new OrderingProvider { ProviderId = "P100", Name = "Rivera, Ana", IsActive = true },
            new OrderingProvider { ProviderId = "P900", Name = "Retired, Doc", IsActive = false });
        db.Users.Add(new User { UserName = "sup.lee", DisplayName = "Pat Lee", IsActive = true });
        await db.SaveChangesAsync();
    }

    private async Task<Override> SaveOverrideAsync(string authorizedBy, AuthorizerDirectoryMatch? preset = null)
    {
        await using var db = _factory.Create();
        var row = new Override
        {
            Action = OverrideAction.EmergencyRelease,
            ContextType = nameof(Issue),
            ContextId = 1,
            RuleCode = "ISS-TEST",
            Reason = "Trauma bay",
            AuthorizedBy = authorizedBy,
            OverriddenUtc = _factory.Clock.UtcNow,
            AuthorizerMatch = preset
        };
        db.Overrides.Add(row);
        await db.SaveChangesAsync();
        return await db.Overrides.AsNoTracking().SingleAsync(o => o.Id == row.Id);
    }

    [Fact]
    public async Task Provider_User_AndUnknown_AreStamped()
    {
        await SeedDirectoryAsync();

        var provider = await SaveOverrideAsync("Rivera, Ana (P100)");
        Assert.Equal(AuthorizerDirectoryMatch.OrderingProvider, provider.AuthorizerMatch);
        Assert.Equal("P100", provider.AuthorizerReference);

        var user = await SaveOverrideAsync("pat lee");
        Assert.Equal(AuthorizerDirectoryMatch.User, user.AuthorizerMatch);
        Assert.Equal("sup.lee", user.AuthorizerReference);

        var unknown = await SaveOverrideAsync("Dr Somebody");
        Assert.Equal(AuthorizerDirectoryMatch.NotInDirectory, unknown.AuthorizerMatch);
        Assert.Null(unknown.AuthorizerReference);
        Assert.Equal("Dr Somebody", unknown.AuthorizedBy);
    }

    [Fact]
    public async Task InactiveProvider_IsNotInDirectory()
    {
        await SeedDirectoryAsync();
        var row = await SaveOverrideAsync("P900");
        Assert.Equal(AuthorizerDirectoryMatch.NotInDirectory, row.AuthorizerMatch);
    }

    [Fact]
    public async Task ExistingMatch_IsNotRecomputed_AndLaterEditsDoNotRestamp()
    {
        await SeedDirectoryAsync();
        var preset = await SaveOverrideAsync("Dr Somebody", AuthorizerDirectoryMatch.Ambiguous);
        Assert.Equal(AuthorizerDirectoryMatch.Ambiguous, preset.AuthorizerMatch);

        // A review later edits the row; the match recorded at override time stays.
        await using var db = _factory.Create();
        var row = await db.Overrides.SingleAsync(o => o.Id == preset.Id);
        row.ReviewNote = "Reviewed";
        row.AuthorizedBy = "P100";
        await db.SaveChangesAsync();
        Assert.Equal(AuthorizerDirectoryMatch.Ambiguous, (await db.Overrides.AsNoTracking().SingleAsync(o => o.Id == preset.Id)).AuthorizerMatch);
    }
}
