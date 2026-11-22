using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PoolComponentsTests
{
    private static string Type(AboGroup abo, RhType rh) => $"{abo} {(rh == RhType.Positive ? "Pos" : "Neg")}";

    // TEST-BB-1397 (mixed-ABO pooling phase 1): components round-trip as JSON, a pool of one group is not mixed and one
    // of two is, the summary counts each group in the order first seen, and unreadable or empty JSON gives none.
    [Fact]
    public void Components_RoundTrip_Summarize_AndDetectMixed()
    {
        var o = new PoolComponent(AboGroup.O, RhType.Positive);
        var a = new PoolComponent(AboGroup.A, RhType.Positive);
        var json = PoolComponents.ToJson([o, o, a, o]);
        Assert.Contains("\"abo\":\"O\"", json);
        var parsed = PoolComponents.Parse(json);
        Assert.Equal([o, o, a, o], parsed);
        Assert.True(PoolComponents.IsMixed(parsed));
        Assert.False(PoolComponents.IsMixed([o, o]));
        Assert.True(PoolComponents.IsMixed([o, new PoolComponent(AboGroup.O, RhType.Negative)]));
        Assert.Equal("4 units: O Pos ×3, A Pos", PoolComponents.Summary(parsed, Type));
        Assert.Empty(PoolComponents.Parse(null));
        Assert.Empty(PoolComponents.Parse("not json"));
    }
}
