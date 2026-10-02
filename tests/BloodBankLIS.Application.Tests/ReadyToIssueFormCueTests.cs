using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class ReadyToIssueFormCueTests
{
    [Fact]
    public void Notices_RepeatTheReadyToIssueMarks()
    {
        var marked = Row(
            specimenBlockReason: "Specimen expired.",
            crossmatchNotice: "A compatible, unexpired crossmatch is required for this product.",
            antigenNotice: "Patient has anti-K; unit is not tested for K and must be antigen-negative.",
            donationRestrictionNotice: "Patient autologous",
            productName: "Red Blood Cells",
            needsSecondAbo: true,
            unitExpiresUtc: new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc),
            unitBloodType: "A+",
            crossmatchExpiresUtc: new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc),
            storageLocationName: "Main Blood Bank Refrigerator",
            specimenAccession: "ACC0001");
        var notices = ReadyToIssueFormCue.Notices(marked);

        Assert.Contains("Specimen expired.", notices);
        Assert.Contains("A compatible, unexpired crossmatch is required for this product.", notices);
        Assert.Contains("Patient has anti-K; unit is not tested for K and must be antigen-negative.", notices);
        Assert.Contains(ReadyToIssueFormCue.SecondAbo, notices);
        Assert.Contains("Patient autologous", notices);
        Assert.Equal("Red Blood Cells", notices[0]);
        Assert.Equal("Unit expires 2026-06-19 12:00", notices[1]);
        Assert.Equal("Unit type A+", notices[2]);
        Assert.Equal("Stored in Main Blood Bank Refrigerator", notices[3]);
        Assert.Equal("Specimen ACC0001", notices[4]);
        Assert.Equal("Crossmatch expires 2026-06-21 12:00", notices[5]);
        var withResult = Row(
            productName: "Red Blood Cells",
            unitExpiresUtc: new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc),
            unitBloodType: "A+",
            crossmatchExpiresUtc: new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc),
            storageLocationName: "Main Blood Bank Refrigerator",
            specimenAccession: "ACC0001",
            latestCrossmatchResult: CrossmatchResult.Compatible);
        Assert.Equal(
            "Crossmatch compatible · Crossmatch expires 2026-06-21 12:00",
            ReadyToIssueFormCue.Notices(withResult)[5]);
        Assert.Equal(
            "Crossmatch incompatible",
            Assert.Single(ReadyToIssueFormCue.Notices(Row(latestCrossmatchResult: CrossmatchResult.Incompatible))));
        Assert.Empty(ReadyToIssueFormCue.Notices(Row(latestCrossmatchResult: CrossmatchResult.NotPerformed)));
        Assert.Equal(
            "Crossmatch compatible · Crossmatch expires 2026-06-21 12:00",
            ReadyToIssueFormCue.CrossmatchWorklistNotice(
                CrossmatchResult.Compatible,
                new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(
            "Crossmatch incompatible",
            ReadyToIssueFormCue.CrossmatchWorklistNotice(CrossmatchResult.Incompatible, null));
        Assert.Null(ReadyToIssueFormCue.CrossmatchWorklistNotice(null, null));
        Assert.Equal("FRIDGE-1", ReadyToIssueFormCue.StorageLocationLabel("  ", "FRIDGE-1"));
        Assert.Null(ReadyToIssueFormCue.StorageLocationNotice(null));

        Assert.Empty(ReadyToIssueFormCue.Notices(Row()));
    }

    private static ReadyToIssueWorkItemDto Row(
        string? specimenBlockReason = null,
        string? crossmatchNotice = null,
        string? antigenNotice = null,
        string? donationRestrictionNotice = null,
        string? productName = null,
        bool needsSecondAbo = false,
        DateTime? unitExpiresUtc = null,
        string? unitBloodType = null,
        DateTime? crossmatchExpiresUtc = null,
        string? storageLocationName = null,
        string? specimenAccession = null,
        CrossmatchResult? latestCrossmatchResult = null) =>
        new(
            1, 2, "MRN", "Kellerman, Diane", "1970-01-01", 3, "U1", "O+",
            false, null, null, false, DateTime.UtcNow,
            ProductAllocationDisplayStatus.Reserved,
            latestCrossmatchResult,
            NeedsSecondAbo: needsSecondAbo,
            SpecimenBlockReason: specimenBlockReason,
            CrossmatchNotice: crossmatchNotice,
            AntigenNotice: antigenNotice,
            DonationRestrictionNotice: donationRestrictionNotice,
            ProductName: productName,
            UnitExpiresUtc: unitExpiresUtc,
            UnitBloodType: unitBloodType,
            CrossmatchExpiresUtc: crossmatchExpiresUtc,
            StorageLocationName: storageLocationName,
            SpecimenAccession: specimenAccession);
}

public class IssuedToNoticeTests
{
    [Fact]
    public void IssuedToNotice_NamesThePerson()
    {
        Assert.Equal("Issued to RN Alvarez", ReadyToIssueFormCue.IssuedToNotice(" RN Alvarez "));
        Assert.Null(ReadyToIssueFormCue.IssuedToNotice("  "));
        Assert.Null(ReadyToIssueFormCue.IssuedToNotice(null));
    }
}

public class CoolerNoticeTests
{
    [Fact]
    public void CoolerNotice_NamesTheContainer()
    {
        Assert.Equal("Cooler CLR-TRAUMA-1", ReadyToIssueFormCue.CoolerNotice(" CLR-TRAUMA-1 "));
        Assert.Null(ReadyToIssueFormCue.CoolerNotice("  "));
        Assert.Null(ReadyToIssueFormCue.CoolerNotice(null));
    }
}

public class RetrospectiveFollowUpNoticeTests
{
    [Fact]
    public void RetrospectiveFollowUpNotice_NamesCustody()
    {
        Assert.Equal(
            "Issued to RN Alvarez · Cooler CLR-TRAUMA-1",
            ReadyToIssueFormCue.RetrospectiveFollowUpNotice(" RN Alvarez ", " CLR-TRAUMA-1 "));
        Assert.Equal("Issued to RN Alvarez", ReadyToIssueFormCue.RetrospectiveFollowUpNotice("RN Alvarez", "  "));
        Assert.Equal("Cooler CLR-TRAUMA-1", ReadyToIssueFormCue.RetrospectiveFollowUpNotice(null, "CLR-TRAUMA-1"));
        Assert.Equal(
            "Issued to RN Alvarez · Cooler CLR-TRAUMA-1 · Location Trauma Bay 2",
            ReadyToIssueFormCue.RetrospectiveFollowUpNotice("RN Alvarez", "CLR-TRAUMA-1", " Trauma Bay 2 "));
        Assert.Equal(
            "Issued to RN Alvarez · Cooler CLR-TRAUMA-1 · Location Trauma Bay 2 · Issued by tech2",
            ReadyToIssueFormCue.RetrospectiveFollowUpNotice("RN Alvarez", "CLR-TRAUMA-1", "Trauma Bay 2", " tech2 "));
        Assert.Equal(
            "Issued to RN Alvarez · Cooler CLR-TRAUMA-1 · Location Trauma Bay 2 · Issued by tech2 · Second verifier witness-tech",
            ReadyToIssueFormCue.RetrospectiveFollowUpNotice("RN Alvarez", "CLR-TRAUMA-1", "Trauma Bay 2", "tech2", " witness-tech "));
        Assert.Equal("Issued by tech2", ReadyToIssueFormCue.RetrospectiveFollowUpNotice(null, " ", null, "tech2"));
        Assert.Equal("Second verifier witness-tech", ReadyToIssueFormCue.RetrospectiveFollowUpNotice(null, " ", null, " ", "witness-tech"));
        Assert.Null(ReadyToIssueFormCue.RetrospectiveFollowUpNotice(" ", null));
    }
}

public class IssuedToLocationNoticeTests
{
    [Fact]
    public void IssuedToLocationNotice_NamesTheWard()
    {
        Assert.Equal("Location Trauma Bay 2", ReadyToIssueFormCue.IssuedToLocationNotice(" Trauma Bay 2 "));
        Assert.Null(ReadyToIssueFormCue.IssuedToLocationNotice("  "));
        Assert.Null(ReadyToIssueFormCue.IssuedToLocationNotice(null));
    }
}

public class IssuedByNoticeTests
{
    [Fact]
    public void IssuedByNotice_NamesTheTechnologist()
    {
        Assert.Equal("Issued by tech2", ReadyToIssueFormCue.IssuedByNotice(" tech2 "));
        Assert.Null(ReadyToIssueFormCue.IssuedByNotice("  "));
        Assert.Null(ReadyToIssueFormCue.IssuedByNotice(null));
    }
}

public class SecondVerifierNoticeTests
{
    [Fact]
    public void SecondVerifierNotice_NamesTheWitness()
    {
        Assert.Equal("Second verifier witness-tech", ReadyToIssueFormCue.SecondVerifierNotice(" witness-tech "));
        Assert.Null(ReadyToIssueFormCue.SecondVerifierNotice("  "));
        Assert.Null(ReadyToIssueFormCue.SecondVerifierNotice(null));
    }
}

public class EmergencyReleaseAuthorizerNoticeTests
{
    [Fact]
    public void EmergencyReleaseAuthorizerNotice_NamesTheAuthorizer()
    {
        Assert.Equal("Authorized by dr-authorizer", ReadyToIssueFormCue.EmergencyReleaseAuthorizerNotice(" dr-authorizer "));
        Assert.Null(ReadyToIssueFormCue.EmergencyReleaseAuthorizerNotice("  "));
        Assert.Null(ReadyToIssueFormCue.EmergencyReleaseAuthorizerNotice(null));
    }
}

public class IssueAppearanceNoticeTests
{
    [Fact]
    public void IssueAppearanceNotice_NamesTheCode()
    {
        Assert.Equal("Appearance Acceptable", ReadyToIssueFormCue.IssueAppearanceNotice(UnitAppearance.Acceptable));
        Assert.Equal("Appearance Hemolysis", ReadyToIssueFormCue.IssueAppearanceNotice(UnitAppearance.Hemolysis));
    }
}

public class WardAppearanceNoticeTests
{
    [Fact]
    public void WardAppearanceNotice_NamesTheCode()
    {
        Assert.Equal("Ward appearance Acceptable", ReadyToIssueFormCue.WardAppearanceNotice(" Acceptable "));
        Assert.Equal("Ward appearance Hemolysis", ReadyToIssueFormCue.WardAppearanceNotice("Hemolysis"));
        Assert.Null(ReadyToIssueFormCue.WardAppearanceNotice("  "));
        Assert.Null(ReadyToIssueFormCue.WardAppearanceNotice(null));
    }
}

public class PtagNoticeTests
{
    [Fact]
    public void PtagNotice_NamesTheCode()
    {
        Assert.Equal("Pickup tag PTAG-OUT", ReadyToIssueFormCue.PtagNotice(" PTAG-OUT "));
        Assert.Null(ReadyToIssueFormCue.PtagNotice("  "));
        Assert.Null(ReadyToIssueFormCue.PtagNotice(null));
    }
}

public class CrossmatchStatusNoticeTests
{
    [Fact]
    public void CrossmatchStatusNotice_NamesTheStatus()
    {
        Assert.Equal("XM at issue Compatible", ReadyToIssueFormCue.CrossmatchStatusNotice(CrossmatchClinicalStatus.Compatible));
        Assert.Equal("XM at issue NotCrossmatchedEmergency", ReadyToIssueFormCue.CrossmatchStatusNotice(CrossmatchClinicalStatus.NotCrossmatchedEmergency));
    }
}

public class EmergencyReleaseReasonNoticeTests
{
    [Fact]
    public void EmergencyReleaseReasonNotice_NamesTheReason()
    {
        Assert.Equal(
            "Release reason: Uncrossmatched release for hemorrhage",
            ReadyToIssueFormCue.EmergencyReleaseReasonNotice(" Uncrossmatched release for hemorrhage "));
        Assert.Null(ReadyToIssueFormCue.EmergencyReleaseReasonNotice("  "));
        Assert.Null(ReadyToIssueFormCue.EmergencyReleaseReasonNotice(null));
    }
}
