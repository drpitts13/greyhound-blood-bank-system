using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-337
public class AuthorizerDirectoryRuleTests
{
    private static readonly AuthorizerDirectoryRule.Entry[] Providers =
    [
        new("P100", "Rivera, Ana"),
        new("P200", "Chen, Wei"),
        new("P300", "Chen, Wei")
    ];

    private static readonly AuthorizerDirectoryRule.Entry[] Users =
    [
        new("jsmith", "Jordan Smith"),
        new("arivera", "Rivera, Ana")
    ];

    [Theory]
    [InlineData("P100")]
    [InlineData("rivera, ana")]
    [InlineData("  Rivera,   Ana ")]
    [InlineData("Rivera, Ana (P100)")]
    public void Provider_MatchesOnIdNameOrSuggestion_AndWinsOverAUserWithTheSameName(string typed)
    {
        var r = AuthorizerDirectoryRule.Match(typed, Providers, Users);
        Assert.Equal(AuthorizerDirectoryMatch.OrderingProvider, r.Match);
        Assert.Equal("P100", r.Reference);
    }

    [Theory]
    [InlineData("JSMITH")]
    [InlineData("Jordan Smith")]
    [InlineData("Jordan Smith (jsmith)")]
    public void User_MatchesOnUserNameNameOrSuggestion(string typed)
    {
        var r = AuthorizerDirectoryRule.Match(typed, Providers, Users);
        Assert.Equal(AuthorizerDirectoryMatch.User, r.Match);
        Assert.Equal("jsmith", r.Reference);
    }

    [Fact]
    public void TwoProvidersWithTheSameName_AreAmbiguous_WithNoReference()
    {
        var r = AuthorizerDirectoryRule.Match("Chen, Wei", Providers, Users);
        Assert.Equal(AuthorizerDirectoryMatch.Ambiguous, r.Match);
        Assert.Null(r.Reference);
        Assert.Equal(AuthorizerDirectoryMatch.OrderingProvider, AuthorizerDirectoryRule.Match("Chen, Wei (P300)", Providers, Users).Match);
    }

    [Theory]
    [InlineData("Dr House")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownOrBlank_IsNotInDirectory(string? typed) =>
        Assert.Equal(AuthorizerDirectoryMatch.NotInDirectory, AuthorizerDirectoryRule.Match(typed, Providers, Users).Match);

    [Fact]
    public void Suggestion_RoundTripsThroughMatch()
    {
        foreach (var entry in Providers.Take(1).Concat(Users.Take(1)))
        {
            Assert.NotEqual(AuthorizerDirectoryMatch.NotInDirectory,
                AuthorizerDirectoryRule.Match(AuthorizerDirectoryRule.Suggestion(entry), Providers, Users).Match);
        }

        Assert.Equal("svc", AuthorizerDirectoryRule.Suggestion(new("svc", null)));
    }

    [Fact]
    public void Describe_UsesPlainWords()
    {
        Assert.Equal("provider P100", AuthorizerDirectoryRule.Describe(AuthorizerDirectoryMatch.OrderingProvider, "P100"));
        Assert.Equal("not in the provider or user directory", AuthorizerDirectoryRule.Describe(AuthorizerDirectoryMatch.NotInDirectory, null));
        Assert.Equal("not checked against the directory", AuthorizerDirectoryRule.Describe(null, null));
    }
}
