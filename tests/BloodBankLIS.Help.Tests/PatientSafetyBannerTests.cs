using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The chart's safety banners, rendered: antibodies (active, historical, known only from another facility), enforced
/// special requirements named one way (Cycle 566), and informational notes apart from them; a requirement no longer in
/// effect is not shown; nothing is drawn when there is nothing to warn about.
/// </summary>
public class PatientSafetyBannerTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly PatientDto Patient = new(1, "MRN0001", "Demo", "Patricia", null, new DateOnly(1970, 1, 1), Sex.Female, PatientStatus.Active, Now, "test");

    private static SpecialRequirementDto Requirement(long id, SpecialTransfusionRequirementType type, string? name, string? antigen = null,
        bool active = true, SpecialRequirementCategory? category = null, string? instruction = null) => new(
        id, 1, type, antigen, "Reason " + id, Now.AddDays(-1), null, active, "tech", Name: name, IsClinicallyActive: active,
        Category: category, Instruction: instruction);

    private static AntibodyDto Antibody(long id, string specificity, bool active = true, string? facility = null, DateOnly? on = null) => new(
        id, 1, null, specificity, AntibodyStatus.Identified, active, null, null, Now.AddDays(-10), "tech", ReportingFacility: facility, IdentifiedOn: on);

    private static readonly SpecialRequirementDto[] Requirements =
    [
        Requirement(1, SpecialTransfusionRequirementType.Irradiated, "Irradiated red cells"),
        Requirement(2, SpecialTransfusionRequirementType.AntigenNegative, null, antigen: "K"),
        Requirement(3, SpecialTransfusionRequirementType.Other, "Call the transfusion physician", category: SpecialRequirementCategory.Informational,
            instruction: "Call before issuing."),
        Requirement(4, SpecialTransfusionRequirementType.Washed, "Washed", active: false)
    ];

    // TEST-BB-667
    [Fact]
    public void ChartHeader_NamesAntibodiesAndRequirements()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var header = Render<PatientChartHeader>(p => p
            .Add(c => c.Patient, Patient)
            .Add(c => c.AntibodyHistory, new List<AntibodyDto>
            {
                Antibody(1, "Anti-K"),
                Antibody(2, "Anti-E", active: false),
                Antibody(3, "Anti-Jka", facility: "St. Mary's Hospital", on: new DateOnly(2025, 2, 1))
            })
            .Add(c => c.ActiveSpecialRequirements, Requirements.Where(r => r.IsClinicallyActive).ToList()));

        Assert.Equal(
            "Antibody history: Anti-K, Anti-E (historical / currently undetectable), Anti-Jka (reported by St. Mary's Hospital, identified 2025-02-01)",
            Normalize(header.Find("[data-header-antibodies]").TextContent));
        Assert.Equal("Special transfusion requirements: Irradiated red cells, Antigen-negative (K)",
            Normalize(header.Find("[data-header-requirements]").TextContent));
        var note = Assert.Single(header.FindAll("[data-special-requirement-banner]"));
        Assert.Equal("Call the transfusion physician: Call before issuing.", Normalize(note.TextContent));
        Assert.DoesNotContain("Washed", header.Markup);
    }

    [Fact]
    public void ChartHeader_NoBanners_WhenNothingToWarnAbout()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var header = Render<PatientChartHeader>(p => p.Add(c => c.Patient, Patient));
        Assert.Empty(header.FindAll("[data-header-antibodies]"));
        Assert.Empty(header.FindAll("[data-header-requirements]"));
        Assert.Empty(header.FindAll("[data-special-requirement-banner]"));
    }

    // TEST-BB-668: the banner where units are picked loads the patient's requirements itself: enforced ones named one way
    // with what happens to a unit that does not meet them, notes apart, and one no longer in effect left out; when the
    // server cannot answer, nothing is drawn.
    [Fact]
    public void RequirementsBanner_WarnsWhereUnitsArePicked()
    {
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/patients/1/special-requirements"] = Requirements.ToList() });
        var banner = Render<PatientRequirementsBanner>(p => p.Add(c => c.PatientId, 1L));
        banner.WaitForAssertion(() => Assert.NotEmpty(banner.FindAll("[data-requirements-enforced]")));
        Assert.Equal(
            "Special requirements: Irradiated red cells, Antigen-negative (K). A unit that does not meet them is refused at reserve and at issue.",
            Normalize(banner.Find("[data-requirements-enforced]").TextContent));
        Assert.Equal("Call the transfusion physician: Call before issuing.", Normalize(Assert.Single(banner.FindAll("[data-requirements-note]")).TextContent));
        Assert.DoesNotContain("Washed", banner.Markup);

        var unknown = Render<PatientRequirementsBanner>(p => p.Add(c => c.PatientId, 2L));
        Assert.Empty(unknown.FindAll("[data-requirements-enforced]"));
        Assert.Empty(unknown.FindAll("[data-requirements-note]"));
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
