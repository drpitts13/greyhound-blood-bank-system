using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-311: patient identifiers at issue and bedside start empty unless the facility allows pre-fill (OCD-039).
public class PatientIdentifierEntryRuleTests
{
    private static readonly DateOnly Dob = new(1988, 6, 30);

    [Fact]
    public void Prefill_Off_LeavesBothFieldsEmpty()
    {
        Assert.Equal((null, null), PatientIdentifierEntryRule.Prefill(false, "MRN0005", Dob));
    }

    [Fact]
    public void Prefill_On_UsesTheRecord()
    {
        Assert.Equal(("MRN0005", "1988-06-30"), PatientIdentifierEntryRule.Prefill(true, "MRN0005", Dob));
        Assert.Equal(("MRN0005", null), PatientIdentifierEntryRule.Prefill(true, "MRN0005", null));
    }

    [Fact]
    public void Policy_ShipsOff_AndIsAnIssueBoolean()
    {
        var def = FacilityPolicyCatalog.Find(FacilityPolicyKeys.PrefillPatientIdentifiers);

        Assert.NotNull(def);
        Assert.Equal("false", def.DefaultValue);
        Assert.Equal(FacilityPolicyValueKind.Boolean, def.Kind);
        Assert.Equal("Issue", def.Category);
        Assert.False(FacilityPolicyCatalog.BoolDefault(FacilityPolicyKeys.PrefillPatientIdentifiers));
    }

    [Fact]
    public void BoolDefault_KeepsOtherPoliciesAsCataloged_AndUnknownAsTrue()
    {
        Assert.True(FacilityPolicyCatalog.BoolDefault(FacilityPolicyKeys.RequireSecondAboForCellularIssue));
        Assert.False(FacilityPolicyCatalog.BoolDefault(FacilityPolicyKeys.PbmEvaluateOrders));
        Assert.True(FacilityPolicyCatalog.BoolDefault("No.Such.Policy"));
    }

    [Theory]
    [InlineData(IdentityTokenType.MedicalRecordNumber, null, PatientIdentifierEntryRule.Hint.Empty)]
    [InlineData(IdentityTokenType.MedicalRecordNumber, "  ", PatientIdentifierEntryRule.Hint.Empty)]
    [InlineData(IdentityTokenType.MedicalRecordNumber, "mrn0005", PatientIdentifierEntryRule.Hint.Matches)]
    [InlineData(IdentityTokenType.MedicalRecordNumber, "MRN0006", PatientIdentifierEntryRule.Hint.DoesNotMatch)]
    [InlineData(IdentityTokenType.DateOfBirth, "1988-06-30", PatientIdentifierEntryRule.Hint.Matches)]
    [InlineData(IdentityTokenType.DateOfBirth, "1988-06-03", PatientIdentifierEntryRule.Hint.DoesNotMatch)]
    [InlineData(IdentityTokenType.DateOfBirth, "not a date", PatientIdentifierEntryRule.Hint.DoesNotMatch)]
    public void Check_AgreesWithTheIssueIdentityRule(IdentityTokenType type, string? entered, PatientIdentifierEntryRule.Hint expected)
    {
        var hint = PatientIdentifierEntryRule.Check(type, entered, "MRN0005", Dob, "Doe", "John");

        Assert.Equal(expected, hint);
        if (expected != PatientIdentifierEntryRule.Hint.Empty)
        {
            var gate = PatientIdentityMatchRule.Matches("MRN0005", Dob, "Doe", "John", new PatientIdentityMatchRule.IdentityToken(type, entered!));
            Assert.Equal(expected == PatientIdentifierEntryRule.Hint.Matches, gate);
        }
    }
}
