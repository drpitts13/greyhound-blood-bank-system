using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyIdentificationParserTests
{
    private static readonly IReadOnlyList<AntibodyCatalogItem> Catalog =
    [
        new(1, "K", "Kell", "anti-K"),
        new(2, "E", "Rh E", "anti-E"),
        new(3, "C", "Rh C", "anti-C"),
        new(4, "c", "Rh c", "anti-c"),
        new(5, "FYA", "Duffy a", "anti-Fya")
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Negative")]
    [InlineData("none identified")]
    [InlineData("No antibodies")]
    public void NegativeOrEmpty_ResolvesNothing(string? value)
    {
        Assert.True(AntibodyIdentificationParser.IsNegativeOrUnidentified(value));
        Assert.Empty(AntibodyIdentificationParser.Resolve(value, Catalog));
    }

    [Fact]
    public void Resolves_CatalogNames_Codes_And_DelimitedList()
    {
        var hits = AntibodyIdentificationParser.Resolve("anti-K, E + anti-Fya", Catalog);

        Assert.Equal(3, hits.Count);
        Assert.Equal("K", hits[0].CatalogItem?.Code);
        Assert.Equal("E", hits[1].CatalogItem?.Code);
        Assert.Equal("FYA", hits[2].CatalogItem?.Code);
    }

    [Fact]
    public void Distinguishes_Rh_C_From_c()
    {
        var big = Assert.Single(AntibodyIdentificationParser.Resolve("anti-C", Catalog));
        var little = Assert.Single(AntibodyIdentificationParser.Resolve("anti-c", Catalog));

        Assert.Equal("C", big.CatalogItem?.Code);
        Assert.Equal("c", little.CatalogItem?.Code);
    }

    [Fact]
    public void Unmatched_AntiToken_IsReturned_WithoutCatalog()
    {
        var hit = Assert.Single(AntibodyIdentificationParser.Resolve("anti-Vel", Catalog));

        Assert.Equal("anti-Vel", hit.Token);
        Assert.Null(hit.CatalogItem);
        Assert.True(AntibodyIdentificationParser.LooksLikeAntibodyToken(hit.Token));
    }

    [Fact]
    public void Format_Joins_NormalizedLabels()
    {
        Assert.Equal("anti-K, anti-E", AntibodyIdentificationParser.Format(["anti-K", " anti-E ", "anti-K"]));
    }
}
