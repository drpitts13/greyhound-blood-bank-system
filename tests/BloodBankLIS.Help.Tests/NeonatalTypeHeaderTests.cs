using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>OCD-074 part two: the chart header agrees with issue about an infant's admission type.</summary>
public class NeonatalTypeHeaderTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly PatientDto Infant = new(1, "MRN-NEO", "Baby", "Ann", null, DateOnly.FromDateTime(Now).AddDays(-10), Sex.Female, PatientStatus.Active, Now, "test");

    private static SpecimenDto Specimen(bool admission, DateTime expires) => new(
        9, "ACC-NEO", 1, "EDTA", null, null, null, null, Now.AddHours(-3), Now.AddHours(-2), expires, SpecimenStatus.Accepted, null,
        NeonatalAdmission: admission);

    private static readonly BloodTypeDto[] OneType =
        [new(1, 1, AboGroup.O, RhType.Positive, "O+", BloodTypeSource.TestResult, null, true, null, Now.AddHours(-1), "tech", SpecimenId: 9)];

    // TEST-BB-875: an infant's only ABO/Rh from this admission's specimen reads "Infant: stands for the admission" with
    // no missing-second-type notice; the same type on an ordinary specimen, or once the admission specimen has expired,
    // shows the notice as before.
    [Fact]
    public void InfantAdmissionType_HasNoSecondTypeNotice()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        IRenderedComponent<PatientChartHeader> Header(SpecimenDto specimen) => Render<PatientChartHeader>(p => p
            .Add(c => c.Patient, Infant)
            .Add(c => c.RequireSecondAbo, true)
            .Add(c => c.TypeHistory, OneType)
            .Add(c => c.Specimens, [specimen]));

        var admission = Header(Specimen(true, Now.AddDays(20)));
        Assert.NotNull(admission.Find("[data-neonatal-type]"));
        Assert.DoesNotContain("needs a second concordant ABO/Rh", admission.Markup);

        Assert.Contains("needs a second concordant ABO/Rh", Header(Specimen(false, Now.AddDays(2))).Markup);
        Assert.Contains("needs a second concordant ABO/Rh", Header(Specimen(true, Now.AddMinutes(-5))).Markup);
    }
}
