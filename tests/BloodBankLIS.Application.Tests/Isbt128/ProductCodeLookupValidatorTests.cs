using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Application.Tests.Isbt128;

public class ProductCodeLookupValidatorTests
{
    private static IReadOnlyDictionary<string, ProductParser.LookupRow> Lookup() =>
        new Dictionary<string, ProductParser.LookupRow>(StringComparer.Ordinal)
        {
            ["E0206"] = new(
                "E0206",
                "RED BLOOD CELLS|CPDA-1/450mL/refg|Irradiated",
                "RedBloodCells",
                null,
                Array.Empty<string>(),
                null,
                false,
                null,
                null,
                "US-PUBLIC-SUBSET-PENDING-ICCBBA"),
            ["E0999"] = new(
                "E0999",
                "Retired product",
                "RedBloodCells",
                null,
                Array.Empty<string>(),
                null,
                false,
                null,
                new DateOnly(2020, 1, 1),
                "US-PUBLIC-SUBSET-PENDING-ICCBBA")
        };

    [Fact]
    public void Validate_Missing_Fails()
    {
        var result = ProductCodeLookupValidator.Validate(null, Lookup());
        Assert.False(result.Success);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }

    [Fact]
    public void Validate_UnknownPdc_Fails()
    {
        var result = ProductCodeLookupValidator.Validate("EXXXX", Lookup());
        Assert.False(result.Success);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }

    [Fact]
    public void Validate_FiveCharPdc_Succeeds()
    {
        var result = ProductCodeLookupValidator.Validate("e0206", Lookup());
        Assert.True(result.Success);
        Assert.Equal("E0206", result.Value!.ProductDescriptionCode);
        Assert.Null(result.Value.ProductCodeData);
    }

    [Fact]
    public void Validate_EightCharProductCodeData_Succeeds()
    {
        var result = ProductCodeLookupValidator.Validate("E0206000", Lookup());
        Assert.True(result.Success);
        Assert.Equal("E0206", result.Value!.ProductDescriptionCode);
        Assert.Equal("E0206000", result.Value.ProductCodeData);
        Assert.Equal("0", result.Value.CollectionTypeCode);
        Assert.Equal("00", result.Value.DivisionCode);
    }

    [Fact]
    public void Validate_RetiredPdc_Fails()
    {
        var result = ProductCodeLookupValidator.Validate("E0999", Lookup(), new DateOnly(2024, 1, 1));
        Assert.False(result.Success);
        Assert.Contains(IsbtErrorCodes.RetiredProductNotAllowed, result.Error);
    }
}
