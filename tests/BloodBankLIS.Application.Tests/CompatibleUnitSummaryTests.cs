using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class CompatibleUnitSummaryTests
{
    private static CompatibleUnitDto Unit(long id, string product) =>
        new(id, $"W{id}", 1, "P", product, false, AboGroup.A, RhType.Positive, "A+", DateTime.UtcNow.AddDays(10));

    // TEST-BB-1023: the compatible-units note names the products, most first, so a list of plasma is not read as red
    // cells (end-user testing pass 14); none says so.
    [Fact]
    public void Note_NamesTheProducts()
    {
        var note = CompatibleUnitSummary.Note([Unit(1, "Fresh Frozen Plasma"), Unit(2, "Fresh Frozen Plasma"), Unit(3, "Red Blood Cells, Irradiated")]);
        Assert.Equal("3 available units are compatible with this patient's type, special requirements, and antibodies: 2 Fresh Frozen Plasma, 1 Red Blood Cells, Irradiated.", note);
        Assert.StartsWith("1 available unit is compatible", CompatibleUnitSummary.Note([Unit(1, "Fresh Frozen Plasma")]));
        Assert.StartsWith("No available unit is compatible", CompatibleUnitSummary.Note([]));
    }

    // TEST-BB-1105 (end-user testing pass 91): units not confirmed antigen-negative for the patient's antibodies are not
    // counted as compatible with them; they are named apart, with why.
    [Fact]
    public void Note_CountsUnitsNotConfirmedAntigenNegativeApart()
    {
        CompatibleUnitDto Flagged(long id, string product, string flag) => Unit(id, product) with { AntigenNegFlag = flag };
        const string untested = BloodBankLIS.Domain.Rules.BloodAttributeCompatibilityRule.UntestedFlag;
        const string positive = BloodBankLIS.Domain.Rules.BloodAttributeCompatibilityRule.NotNegativeFlag;

        Assert.Equal(
            "2 available units are compatible with this patient's type, special requirements, and antibodies: 2 Fresh Frozen Plasma. "
            + "2 more match the type and special requirements but are not confirmed antigen-negative for the patient's antibodies (not tested): 2 Red Blood Cells, Leukoreduced. "
            + "Type them first, or reserve one only with an authorized override.",
            CompatibleUnitSummary.Note([Unit(1, "Fresh Frozen Plasma"), Unit(2, "Fresh Frozen Plasma"),
                Flagged(3, "Red Blood Cells, Leukoreduced", untested), Flagged(4, "Red Blood Cells, Leukoreduced", untested)]));
        Assert.StartsWith("No available unit is confirmed compatible with this patient's antibodies. 1 more matches",
            CompatibleUnitSummary.Note([Flagged(3, "Red Blood Cells, Leukoreduced", positive)]));
        Assert.Contains("(1 not tested, 1 antigen-positive)", CompatibleUnitSummary.Note([Flagged(3, "RBC", untested), Flagged(4, "RBC", positive)]));
    }
}
