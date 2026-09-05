using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyIdentificationCatalogResolverTests
{
    private static readonly IReadOnlyList<AntibodyCatalogItem> Catalog =
    [
        new(1, "K", "Kell", "anti-K"),
        new(2, "E", "Rh E", "anti-E")
    ];

    [Fact]
    public void Resolves_RequestedCatalogId()
    {
        var resolved = AntibodyIdentificationCatalogResolver.Resolve(1, "typed something else", Catalog);

        Assert.True(resolved.CatalogMatched);
        Assert.Equal(1, resolved.DefinitionId);
        Assert.Equal("anti-K", resolved.Specificity);
    }

    [Fact]
    public void Resolves_FreeTextAntiK()
    {
        var resolved = AntibodyIdentificationCatalogResolver.Resolve(null, "anti-K", Catalog);

        Assert.True(resolved.CatalogMatched);
        Assert.Equal(1, resolved.DefinitionId);
        Assert.Equal("anti-K", resolved.Specificity);
    }

    [Fact]
    public void UnmatchedIdentified_IsWarning()
    {
        var resolved = AntibodyIdentificationCatalogResolver.Resolve(null, "anti-Vel", Catalog);
        var warning = AntibodyIdentificationCatalogResolver.EvaluateIdentifiedCatalog(
            AntibodyIdClassification.Identified, resolved.CatalogMatched, resolved.Specificity);

        Assert.False(resolved.CatalogMatched);
        Assert.Equal(RuleSeverity.Warning, warning.Severity);
        Assert.Equal(AntibodyIdentificationCatalogResolver.UnmatchedIdentifiedCode, warning.Code);
    }

    [Fact]
    public void PossibleUnmatched_IsNotWarning()
    {
        var warning = AntibodyIdentificationCatalogResolver.EvaluateIdentifiedCatalog(
            AntibodyIdClassification.Possible, catalogMatched: false, "anti-Vel");

        Assert.Equal(RuleSeverity.Pass, warning.Severity);
    }

    [Fact]
    public void AmbiguousList_DoesNotPickOne()
    {
        var resolved = AntibodyIdentificationCatalogResolver.Resolve(null, "anti-K, anti-E", Catalog);

        Assert.False(resolved.CatalogMatched);
        Assert.Null(resolved.DefinitionId);
    }
}
