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
}
