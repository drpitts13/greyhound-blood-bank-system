using BloodBankLIS.Application.Isbt128;

namespace BloodBankLIS.Application.Tests.Isbt128;

public class IsbtLookupCatalogPreferenceTests
{
    private sealed record Row(string Code, bool IsPlaceholder, DateOnly? RetiredDate, string Label);

    [Fact]
    public void PreferLicensed_PicksNonPlaceholder_ThenNonRetired()
    {
        Row[] rows =
        [
            new("T0001", IsPlaceholder: true, RetiredDate: null, "placeholder"),
            new("T0001", IsPlaceholder: false, RetiredDate: new DateOnly(2020, 1, 1), "licensed-retired"),
            new("T0001", IsPlaceholder: false, RetiredDate: null, "licensed-live")
        ];

        var preferred = IsbtLookupCatalog.PreferLicensed(rows, r => r.Code, r => r.IsPlaceholder, r => r.RetiredDate);
        Assert.Single(preferred);
        Assert.Equal("licensed-live", preferred[0].Label);
    }
}
