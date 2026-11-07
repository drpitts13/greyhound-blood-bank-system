using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-466: a possible duplicate needs the same date of birth and the same last name with the same first initial
// (punctuation, spacing, and case ignored), or first and last names swapped.
public class PossibleDuplicatePatientRuleTests
{
    private static readonly DateOnly Dob = new(1975, 6, 1);

    [Theory]
    [InlineData("Smith", "Robert", "smith", "Bob", false)]
    [InlineData("Smith", "Robert", "SMITH", "Rob", true)]
    [InlineData("O'Neil-Smith", "Ann", "ONEIL SMITH", "anne", true)]
    [InlineData("Smith", "Robert", "Robert", "Smith", true)]
    [InlineData("Smith", "Robert", "Smyth", "Robert", false)]
    [InlineData("Smith", "", "Smith", "Robert", false)]
    public void SameBirthday_MatchesOnLastNameAndInitial_OrSwappedNames(string last, string first, string otherLast, string otherFirst, bool expected) =>
        Assert.Equal(expected, PossibleDuplicatePatientRule.IsPossibleDuplicate(last, first, Dob, otherLast, otherFirst, Dob));

    [Fact]
    public void DifferentBirthday_IsNeverAMatch() =>
        Assert.False(PossibleDuplicatePatientRule.IsPossibleDuplicate("Smith", "Robert", Dob, "Smith", "Robert", Dob.AddDays(1)));

    [Fact]
    public void Message_NamesEachMatch() =>
        Assert.Equal(
            "This may be a patient who is already registered: MRN 1; MRN 2. Open that chart, or confirm this is a different person.",
            PossibleDuplicatePatientRule.Message(["MRN 1", "MRN 2"]));
}
