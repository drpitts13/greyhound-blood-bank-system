using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-325: facility MTP packs (none shipped) validate their contents; round progress is guidance only.
public class MtpPackRuleTests
{
    private static MtpPackDefinition Pack(int rbc = 4, int plasma = 4, int plt = 1, int cryo = 0, int wb = 0, string code = "P1", string name = "Pack") =>
        new() { Code = code, Name = name, RedCellUnits = rbc, PlasmaUnits = plasma, PlateletUnits = plt, CryoUnits = cryo, WholeBloodUnits = wb };

    [Fact]
    public void ValidPack_Passes() =>
        Assert.False(MtpPackRule.Validate(Pack(), duplicateCode: false).IsHardStopped);

    [Fact]
    public void AllZero_IsRefused() =>
        Assert.True(MtpPackRule.Validate(Pack(0, 0, 0), duplicateCode: false).IsHardStopped);

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void OutOfRange_IsRefused(int rbc) =>
        Assert.True(MtpPackRule.Validate(Pack(rbc), duplicateCode: false).IsHardStopped);

    [Theory]
    [InlineData("", "Pack")]
    [InlineData("P1", " ")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU", "Pack")]
    public void MissingOrLongCodeOrName_IsRefused(string code, string name) =>
        Assert.True(MtpPackRule.Validate(Pack(code: code, name: name), duplicateCode: false).IsHardStopped);

    [Fact]
    public void DuplicateCode_IsRefused() =>
        Assert.True(MtpPackRule.Validate(Pack(), duplicateCode: true).IsHardStopped);

    [Fact]
    public void Progress_NothingSent_IsRoundOne()
    {
        var progress = MtpPackRule.Progress(Pack(), new Dictionary<ComponentClass, int>());
        Assert.Equal(0, progress.CompletedRounds);
        Assert.Equal(1, progress.CurrentRound);
        Assert.Equal(3, progress.Components.Count);
        Assert.All(progress.Components, c => Assert.Equal(0, c.InCurrentRound));
    }

    [Fact]
    public void Progress_CountsCompletedRoundsByTheSlowestComponent()
    {
        var sent = new Dictionary<ComponentClass, int>
        {
            [ComponentClass.RedBloodCells] = 9,
            [ComponentClass.Plasma] = 5,
            [ComponentClass.Platelets] = 1
        };

        var progress = MtpPackRule.Progress(Pack(), sent);

        Assert.Equal(1, progress.CompletedRounds);
        Assert.Equal(2, progress.CurrentRound);
        Assert.Equal(5, Assert.Single(progress.Components, c => c.ComponentClass == ComponentClass.RedBloodCells).InCurrentRound);
        Assert.Equal(1, Assert.Single(progress.Components, c => c.ComponentClass == ComponentClass.Plasma).InCurrentRound);
        Assert.Equal(0, Assert.Single(progress.Components, c => c.ComponentClass == ComponentClass.Platelets).InCurrentRound);
    }

    [Fact]
    public void Progress_IgnoresComponentsOutsideThePack()
    {
        var progress = MtpPackRule.Progress(Pack(4, 4, 0), new Dictionary<ComponentClass, int> { [ComponentClass.Platelets] = 3 });
        Assert.DoesNotContain(progress.Components, c => c.ComponentClass == ComponentClass.Platelets);
    }
}
