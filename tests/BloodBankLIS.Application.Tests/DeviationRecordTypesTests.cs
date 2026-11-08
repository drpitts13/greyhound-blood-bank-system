using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Search;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-492: every record the lookup can find by scan or identifier maps to a deviation record type, and the stored
// types are the ones a record's page asks for.
public class DeviationRecordTypesTests
{
    [Fact]
    public void EveryLookupKind_MapsToARecordType()
    {
        Assert.Equal(
            ["Patient", "BloodUnit", "Specimen", "Order", "RhigEvaluation", "BloodUnit"],
            Enum.GetValues<LookupHitKind>().Select(DeviationRecordTypes.FromLookup));
    }

    [Fact]
    public void RecordPages_FindOlderSpellings()
    {
        Assert.Equal(["BloodUnit", "Unit"], DeviationRecordTypes.Aliases(DeviationRecordTypes.BloodUnit));
        Assert.Equal(["Patient"], DeviationRecordTypes.Aliases(DeviationRecordTypes.Patient));
        Assert.Equal(["Hl7", "Hl7Message"], DeviationRecordTypes.Aliases("Hl7Message"));
    }
}
