using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-645: a requirement reads the same on every screen: the catalog name, or the type's catalog wording (never the
// code name), with the antigen in brackets unless the name already says it.
public class SpecialRequirementDisplayTests
{
    [Fact]
    public void Display_OneWordingForEveryScreen()
    {
        Assert.Equal("CMV-negative", SpecialRequirementLabel.Display(null, SpecialTransfusionRequirementType.CmvNegative, null));
        Assert.Equal("Antigen-negative (K)", SpecialRequirementLabel.Display(" ", SpecialTransfusionRequirementType.AntigenNegative, " K "));
        Assert.Equal("Irradiated", SpecialRequirementLabel.Display(" Irradiated ", SpecialTransfusionRequirementType.Irradiated, null));
        Assert.Equal("K-negative", SpecialRequirementLabel.Display("K-negative", SpecialTransfusionRequirementType.AntigenNegative, "K"));
        Assert.Equal("Jka antigen negative", SpecialRequirementLabel.Display("Jka antigen negative", SpecialTransfusionRequirementType.AntigenNegative, "jka"));
        Assert.Equal("Other", SpecialRequirementLabel.Display(null, SpecialTransfusionRequirementType.Other, null));
        Assert.Equal("Leukoreduced", SpecialRequirementLabel.TypeName(SpecialTransfusionRequirementType.Leukoreduced));
        Assert.Equal("Washed", SpecialRequirementLabel.TypeName(SpecialTransfusionRequirementType.Washed));
    }
}
