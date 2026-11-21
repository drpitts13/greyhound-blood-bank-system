using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Example test definitions that show what the catalog can describe beyond the starter tests: titers, a numeric
/// result with units and limits, a DAT with IgG and C3, an elution, a weak D panel, and an Rh/Kell phenotype. Each is
/// seeded once by code as an inactive draft: the medical director reviews, edits, and activates the ones the
/// laboratory uses (OCD-065). No limit, endpoint, or score here is a clinical rule.
/// </summary>
public static partial class DatabaseSeeder
{
    public const string ExampleTestReason = "Example for medical director review (OCD-065).";

    private static async Task EnsureExampleTestsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await EnsureExampleSubtestsAsync(context, ct);
        var examples = new List<TestDefinition>
        {
            Example("ABTITER", "Antibody titer", TestCategory.AntibodyTiter, ResultValueType.Titer, d =>
            {
                d.TiterStartDilution = 1;
                d.TiterEndDilution = 2048;
                d.TiterPhaseCode = "AHG";
                d.TiterEndpointMinGrade = "1+";
                d.TiterScored = true;
                d.ContributesToAntibodyHistory = false;
            }),
            Example("ABOTITER", "ABO antibody titer", TestCategory.AntibodyTiter, ResultValueType.Titer, d =>
            {
                d.TiterStartDilution = 1;
                d.TiterEndDilution = 2048;
                d.TiterPhaseCode = "IS";
                d.TiterEndpointMinGrade = "1+";
            }),
            Example("FMHQ", "Fetal bleed quantitation", TestCategory.Other, ResultValueType.Numeric, d =>
            {
                // Units, precision, and the bounds of a percentage only; reference and critical limits are the SME's.
                d.ResultUnits = "%";
                d.DecimalPlaces = 2;
                d.ReportableLow = 0;
                d.ReportableHigh = 100;
            }),
            Example("DATIGG", "DAT with IgG and C3", TestCategory.DirectAntiglobulinTest, ResultValueType.Subtest, d =>
            {
                d.PanelSubtestsJson = PanelSubtestAssignments.ToJson(
                [
                    new PanelSubtestAssignment("IGG", true, 1),
                    new PanelSubtestAssignment("C3D", true, 2),
                    new PanelSubtestAssignment("DATCTL", true, 3)
                ]);
                d.InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(
                [
                    Row("Negative", "Negative", ("IGG", ReactionPolarity.Negative), ("C3D", ReactionPolarity.Negative), ("DATCTL", ReactionPolarity.Negative)),
                    Row("IgG", "Positive (IgG)", ("IGG", ReactionPolarity.Positive), ("C3D", ReactionPolarity.Negative), ("DATCTL", ReactionPolarity.Negative)),
                    Row("C3", "Positive (C3)", ("IGG", ReactionPolarity.Negative), ("C3D", ReactionPolarity.Positive), ("DATCTL", ReactionPolarity.Negative)),
                    Row("IgGC3", "Positive (IgG and C3)", ("IGG", ReactionPolarity.Positive), ("C3D", ReactionPolarity.Positive), ("DATCTL", ReactionPolarity.Negative)),
                    Row("Invalid", "Invalid (control positive)", InterpretationMatchMode.AnyPositive, ("DATCTL", ReactionPolarity.Positive))
                ]);
                d.ContributesToCompatibility = true;
            }),
            Example("ELUTION", "Elution", TestCategory.Elution, ResultValueType.Subtest, d =>
            {
                string[] phases = ["AHG", "CC"];
                d.PanelSubtestsJson = PanelSubtestAssignments.ToJson(
                [
                    new PanelSubtestAssignment("Cell1", true, 1, phases),
                    new PanelSubtestAssignment("Cell2", true, 2, phases),
                    new PanelSubtestAssignment("Cell3", true, 3, phases),
                    new PanelSubtestAssignment("LASTWASH", true, 4, phases)
                ]);
                var cells = new[] { "Cell1", "Cell2", "Cell3" };
                d.InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(
                [
                    Row("Negative", "Negative",
                        cells.Append("LASTWASH").Select(c => (PhaseResultKeys.Compose(c, "AHG"), ReactionPolarity.Negative)).ToArray()),
                    Row("Positive", "Positive", InterpretationMatchMode.AnyPositive,
                        cells.Select(c => (PhaseResultKeys.Compose(c, "AHG"), ReactionPolarity.Positive)).ToArray()),
                    Row("Invalid", "Invalid (last wash positive)", InterpretationMatchMode.AnyPositive,
                        (PhaseResultKeys.Compose("LASTWASH", "AHG"), ReactionPolarity.Positive))
                ]);
            }),
            Example("WEAKDP", "Weak D test panel", TestCategory.AboRh, ResultValueType.Subtest, d =>
            {
                d.PanelSubtestsJson = PanelSubtestAssignments.ToJson(
                [
                    new PanelSubtestAssignment(AboRhPanelSubtestCodes.AntiD, true, 1, ["IS", "AHG", "CC"]),
                    new PanelSubtestAssignment(AboRhPanelSubtestCodes.Control, true, 2, ["AHG", "CC"])
                ]);
                var antiDIs = PhaseResultKeys.Compose(AboRhPanelSubtestCodes.AntiD, "IS");
                var antiDAhg = PhaseResultKeys.Compose(AboRhPanelSubtestCodes.AntiD, "AHG");
                var controlAhg = PhaseResultKeys.Compose(AboRhPanelSubtestCodes.Control, "AHG");
                d.InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(
                [
                    Row("Negative", "Weak D negative", (antiDIs, ReactionPolarity.Negative), (antiDAhg, ReactionPolarity.Negative), (controlAhg, ReactionPolarity.Negative)),
                    Row("Positive", "Weak D positive", (antiDAhg, ReactionPolarity.Positive), (controlAhg, ReactionPolarity.Negative)),
                    Row("Invalid", "Invalid (control positive)", InterpretationMatchMode.AnyPositive, (controlAhg, ReactionPolarity.Positive))
                ]);
            })
        };

        // Rh/Kell phenotype from the antigens already in the catalog (the catalog has no "e" yet).
        var phenotype = await context.BloodAttributeDefinitions
            .Where(a => a.IsActive && (a.Code == "C" || a.Code == "c" || a.Code == "E" || a.Code == "K"))
            .OrderBy(a => a.SortOrder)
            .ToListAsync(ct);
        if (phenotype.Count > 0)
        {
            examples.Add(Example("RHKPHENO", "Rh and Kell phenotype", TestCategory.AntigenTyping, ResultValueType.BloodAttribute, d =>
            {
                d.BloodAttributeScopeJson = BloodAttributeScope.Serialize(phenotype.Select(a => new BloodAttributeScopeEntry(a.Code)));
                d.BloodAttributeScopeKind = BloodAttributeKind.Antigen;
                d.ContributesToCompatibility = true;
            }));
        }

        var existing = (await context.TestDefinitions.Select(t => t.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = examples.Where(e => !existing.Contains(e.Code)).ToList();
        if (added.Count > 0)
        {
            context.TestDefinitions.AddRange(added);
            await context.SaveChangesAsync(ct);
        }
    }

    private static TestDefinition Example(string code, string name, TestCategory category, ResultValueType valueType, Action<TestDefinition> configure)
    {
        var definition = new TestDefinition
        {
            Code = code,
            Name = name,
            Category = category,
            ResultValueType = valueType,
            VerificationRequired = true,
            IsActive = false,
            IsDraft = true,
            Version = 1,
            ChangeReason = ExampleTestReason
        };
        configure(definition);
        return definition;
    }

    private static InterpretationLogicRow Row(string key, string label, params (string Key, ReactionPolarity Polarity)[] expectations) =>
        Row(key, label, InterpretationMatchMode.AllMatch, expectations);

    private static InterpretationLogicRow Row(
        string key, string label, InterpretationMatchMode mode, params (string Key, ReactionPolarity Polarity)[] expectations) =>
        new(key, label, expectations.ToDictionary(e => e.Key, e => e.Polarity, StringComparer.OrdinalIgnoreCase), mode);

    /// <summary>Graded subtests the examples use: anti-IgG, anti-C3d, the DAT control, and the elution last wash.</summary>
    private static async Task EnsureExampleSubtestsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var choicesJson = SubtestChoiceDefinitions.ToJson(SubtestChoiceDefinitions.DefaultGradedReaction());
        var needed = new (string Code, string Name)[]
        {
            ("IGG", "Anti-IgG"),
            ("C3D", "Anti-C3d"),
            ("DATCTL", "DAT control"),
            ("LASTWASH", "Last wash")
        };

        var changed = false;
        foreach (var (code, name) in needed)
        {
            if (await context.SubtestDefinitions.AnyAsync(s => s.Code == code, ct))
            {
                continue;
            }

            context.SubtestDefinitions.Add(new SubtestDefinition
            {
                Code = code,
                Name = name,
                ResultType = SubtestResultType.GradedReaction,
                ChoicesJson = choicesJson,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = DateTime.UtcNow,
                Version = 1
            });
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Two anti-K titers three weeks apart for the anti-K demo patient (MRN0004), 1:8 and then 1:32, so the chart's Test
    /// History shows a titer and its trend. The earlier titer has its own older tube. Written directly, as other demo
    /// results are; the ABTITER definition stays a draft. Added once.
    /// </summary>
    private static async Task EnsureTiterDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.TestResults.AnyAsync(r => r.TestCode == "ABTITER", ct))
        {
            return;
        }

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0004", ct);
        var current = patient is null
            ? null
            : await context.Specimens.Where(s => s.PatientId == patient.Id).OrderByDescending(s => s.CollectedUtc).FirstOrDefaultAsync(ct);
        if (patient is null || current is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var earlier = new Specimen
        {
            AccessionNumber = "ACC0012-T1",
            PatientId = patient.Id,
            EncounterId = current.EncounterId,
            SpecimenType = current.SpecimenType,
            CollectedUtc = now.AddDays(-21),
            ReceivedUtc = now.AddDays(-21).AddHours(1),
            ExpiresUtc = now.AddDays(-18),
            Status = SpecimenStatus.Accepted,
            Collector = "RN Patel",
            Comment = "Demo: earlier titer tube."
        };
        context.Specimens.Add(earlier);
        await context.SaveChangesAsync(ct);

        var settings = new TiterSettings(1, 2048, "AHG", "1+", Scored: true);
        TiterResultValue Titer(params string[] grades) =>
            TiterRule.Evaluate(settings, grades.Select((g, i) => new TiterReading(1 << i, g))).Value!;
        var eight = Titer("3+", "2+", "1+", "1+", "0");
        var thirtyTwo = Titer("4+", "3+", "2+", "2+", "1+", "1+", "0");
        context.TestResults.AddRange(
            DemoTiter(patient.Id, earlier.Id, eight, now.AddDays(-21).AddHours(3)),
            DemoTiter(patient.Id, current.Id, thirtyTwo, now.AddHours(-1)));
        await context.SaveChangesAsync(ct);
    }

    private static TestResult DemoTiter(long patientId, long specimenId, TiterResultValue titer, DateTime verifiedUtc) => new()
    {
        PatientId = patientId,
        SpecimenId = specimenId,
        TestCode = "ABTITER",
        Version = 1,
        Value = titer.Serialize(),
        Interpretation = titer.Interpretation,
        Status = ResultStatus.Verified,
        Source = ResultSource.Manual,
        EnteredBy = "tech1",
        EnteredUtc = verifiedUtc.AddMinutes(-20),
        VerifiedBy = "supervisor1",
        VerifiedUtc = verifiedUtc
    };
}
