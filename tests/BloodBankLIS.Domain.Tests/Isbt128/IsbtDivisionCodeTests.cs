using BloodBankLIS.Domain.Isbt128;

namespace BloodBankLIS.Domain.Tests.Isbt128;

public class IsbtDivisionCodeTests
{
    [Fact]
    public void TryAllocate_Undivided_Yields0AAnd0B()
    {
        Assert.True(IsbtDivisionCode.TryAllocate("00", 2, null, out var allocated, out var code, out var message));
        Assert.Equal(["0A", "0B"], allocated);
        Assert.Null(code);
        Assert.Null(message);
    }

    [Fact]
    public void TryAllocate_MissingDivision_TreatsAsUndivided()
    {
        Assert.True(IsbtDivisionCode.TryAllocate(null, 2, null, out var allocated, out _, out _));
        Assert.Equal(["0A", "0B"], allocated);
    }

    [Fact]
    public void TryAllocate_ThreeChildren_Includes0C()
    {
        Assert.True(IsbtDivisionCode.TryAllocate("00", 3, null, out var allocated, out _, out _));
        Assert.Equal(["0A", "0B", "0C"], allocated);
    }

    [Fact]
    public void TryAllocate_FirstLevel0A_YieldsAaAb()
    {
        Assert.True(IsbtDivisionCode.TryAllocate("0A", 2, null, out var allocated, out _, out _));
        Assert.Equal(["Aa", "Ab"], allocated);
    }

    [Fact]
    public void TryAllocate_FirstLevel0B_YieldsBaBb()
    {
        Assert.True(IsbtDivisionCode.TryAllocate("0B", 2, null, out var allocated, out _, out _));
        Assert.Equal(["Ba", "Bb"], allocated);
    }

    [Fact]
    public void TryAllocate_SkipsUsedSiblingCodes()
    {
        Assert.True(IsbtDivisionCode.TryAllocate("00", 2, ["0A"], out var allocated, out _, out _));
        Assert.Equal(["0B", "0C"], allocated);
    }

    [Fact]
    public void TryAllocate_SecondLevel_IsBlocked()
    {
        Assert.False(IsbtDivisionCode.TryAllocate("Aa", 2, null, out var allocated, out var code, out var message));
        Assert.Empty(allocated);
        Assert.Equal(IsbtDivisionCode.LevelExceededCode, code);
        Assert.Contains("second ISBT division", message);
    }

    [Fact]
    public void TryAllocate_ExhaustedAlphabet_IsBlocked()
    {
        var used = Enumerable.Range(0, 26).Select(i => "0" + (char)('A' + i)).ToArray();
        Assert.False(IsbtDivisionCode.TryAllocate("00", 1, used, out _, out var code, out _));
        Assert.Equal(IsbtDivisionCode.DivisionExhaustedCode, code);
    }

    [Fact]
    public void BuildProductCodeData_AssemblesEightCharacters()
    {
        Assert.Equal("E0336V0A", IsbtDivisionCode.BuildProductCodeData("E0336", "V", "0A"));
    }

    [Fact]
    public void ResolveBaseProductDescriptionCode_SamePdc_KeepsSource()
    {
        Assert.Equal("E0336", IsbtDivisionCode.ResolveBaseProductDescriptionCode("E0336", "E0336"));
    }

    [Fact]
    public void ResolveBaseProductDescriptionCode_ChangedPdc_UsesTarget()
    {
        Assert.Equal("E0336", IsbtDivisionCode.ResolveBaseProductDescriptionCode("E0023", "E0336"));
    }

    [Fact]
    public void ResolveCollectionType_DefaultsToV()
    {
        Assert.Equal("V", IsbtDivisionCode.ResolveCollectionType(null, null));
    }

    [Fact]
    public void ResolveCollectionType_PreservesExistingProductData()
    {
        Assert.Equal("V", IsbtDivisionCode.ResolveCollectionType(null, "E0336V0A"));
        Assert.Equal("0", IsbtDivisionCode.ResolveCollectionType(null, "E0336000"));
    }

    [Fact]
    public void ResolveDivision_ReadsFromProductCodeData()
    {
        Assert.Equal("0A", IsbtDivisionCode.ResolveDivision(null, "E0336V0A"));
        Assert.Equal("00", IsbtDivisionCode.ResolveDivision(null, null));
    }

    [Fact]
    public void ChangedProduct_ThenDivision_BuildsTargetChildren()
    {
        var pdc = IsbtDivisionCode.ResolveBaseProductDescriptionCode("E0023", "E0336");
        var collection = IsbtDivisionCode.ResolveCollectionType(null, "E0023V00");
        Assert.True(IsbtDivisionCode.TryAllocate("00", 2, null, out var divisions, out _, out _));

        Assert.Equal("E0336V0A", IsbtDivisionCode.BuildProductCodeData(pdc!, collection, divisions[0]));
        Assert.Equal("E0336V0B", IsbtDivisionCode.BuildProductCodeData(pdc!, collection, divisions[1]));
    }
}
