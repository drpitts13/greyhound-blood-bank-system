using BloodBankLIS.Application.Search;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-501: a record found by scan maps to the entity type the audit trail stores, for every lookup kind.
public class LookupRecordTypeTests
{
    [Fact]
    public void EveryLookupKind_MapsToTheAuditEntityType() =>
        Assert.Equal(["Patient", "BloodUnit", "Specimen", "Order", "RhigEvaluation", "BloodUnit", "Issue", "ReagentLot", "Deviation", "InstrumentMessageLog", "BloodDelivery", "BloodUnit", "Hl7MessageLog", "Equipment", "InventoryLocation"], Enum.GetValues<LookupHitKind>().Select(LookupRecordType.For));
}
