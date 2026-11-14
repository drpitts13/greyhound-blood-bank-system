using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    public const string EchoInterfaceName = "ImmuLINK Echo";
    public const int EchoListenPort = 2576;

    public static async Task SeedInstrumentInterfacesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.InstrumentInterfaces.AnyAsync(i => i.Name == EchoInterfaceName, ct))
        {
            await EnsureEchoTranslationsAsync(context, ct);
            return;
        }

        var iface = new InstrumentInterface
        {
            Name = EchoInterfaceName,
            IsEnabled = true,
            Environment = "Development",
            Version = 1,
            InterpretationMatch = InstrumentInterpretationMatch.AnalyteEqualsAssay,
            Connection = new InstrumentConnection { Port = EchoListenPort },
            FieldMappings = EchoFieldMappings(),
            Translations = EchoCodeTranslations().Concat(EchoValueTranslations()).ToList(),
            Rules = EchoRules().Where(r => !EchoRulesNowTranslations.Contains(r.Name)).ToList()
        };

        context.InstrumentInterfaces.Add(iface);
        await context.SaveChangesAsync(ct);
    }

    private static List<InstrumentFieldMapping> EchoFieldMappings() =>
        InstrumentDataItemCatalog.All.Select(item => new InstrumentFieldMapping
        {
            DataItemKey = item.Key,
            AstmPath = item.DefaultAstmPath,
            IsRequired = item.Required
        }).ToList();

    /// <summary>The Echo's assay and analyte codes (what the retired test mapping held).</summary>
    private static List<InstrumentValueTranslation> EchoCodeTranslations()
    {
        var rows = new List<InstrumentValueTranslation>();
        var order = 0;

        void Assay(string instrument, string local, bool sentToInstrument = true)
        {
            rows.Add(Translation(InstrumentTranslationKeys.ResultTestCode, instrument, local, order));
            if (sentToInstrument)
            {
                rows.Add(Translation(InstrumentTranslationKeys.OrderTestCode, instrument, local, order, direction: InterfaceTranslationDirection.Outbound));
            }

            order++;
        }

        void Analyte(string instrument, string local, int sort) =>
            rows.Add(Translation(InstrumentTranslationKeys.SubtestCode, instrument, local, sort));

        Assay("Group", "ABORH");
        Assay("Group2", "ABORH", sentToInstrument: false);
        Assay("Pediatric", "ABORH", sentToInstrument: false);
        Assay("Confirm", "ABORH", sentToInstrument: false);
        Assay("Donor", "ABORH-RETYPE");
        Assay("Screen", "ABSC");
        Assay("Crossmatch", "XM");
        Assay("DAT", "DAT");
        Assay("Weak D", "ABORH", sentToInstrument: false);
        Assay("Ready ID", "ABID");

        Analyte("Anti-A", AboRhPanelSubtestCodes.AntiA, 1);
        Analyte("Anti-B", AboRhPanelSubtestCodes.AntiB, 2);
        Analyte("Anti-D1", AboRhPanelSubtestCodes.AntiD, 3);
        Analyte("Anti-D2", AboRhPanelSubtestCodes.AntiD, 4);
        Analyte("A1 Cells", AboRhPanelSubtestCodes.ACells, 5);
        Analyte("B Cells", AboRhPanelSubtestCodes.BCells, 6);
        Analyte("Mono Ctrl", AboRhPanelSubtestCodes.Control, 7);
        Analyte("Anti-D", AboRhPanelSubtestCodes.WeakD, 8);
        Analyte("Screen 1", "Cell1", 10);
        Analyte("Screen 2", "Cell2", 11);
        Analyte("Screen 3", "Cell3", 12);
        Analyte("IgG XM", "AHG", 20);
        Analyte("Ind Ctrl", "CC", 21);
        Analyte("DAT Rxn", "DAT", 30);
        return rows;
    }

    /// <summary>
    /// Echo values that need saying explicitly: the screen's positive control and the crossmatch donor record are kept
    /// under the instrument's own code (as before), the ABO/Rh and crossmatch interpretations, and the error codes: N is
    /// the ASTM normal end of message, W is set by the "Reaction error to NT" rule. Examples only (OCD-066).
    /// </summary>
    private static List<InstrumentValueTranslation> EchoValueTranslations()
    {
        var rows = new List<InstrumentValueTranslation>
        {
            Translation(InstrumentTranslationKeys.SubtestCode, "Pos Ctrl", InstrumentTranslationKeys.Keep, 13),
            Translation(InstrumentTranslationKeys.SubtestCode, "Donor", InstrumentTranslationKeys.Keep, 22),
            Translation(InstrumentTranslationKeys.Interpretation, "IgG Comp (Check ABO Comp)", "Compatible", 40, context: "XM"),
            ErrorCode("N", InstrumentErrorAction.Ignore, 1, EchoErrorMeanings["N"]),
            ErrorCode("W", InstrumentErrorAction.Warn, 2, EchoErrorMeanings["W"])
        };
        var sort = 30;
        foreach (var abo in new[] { "A", "B", "O", "AB" })
        {
            rows.Add(Translation(InstrumentTranslationKeys.Interpretation, $"{abo} Pos", $"{abo}|Positive", sort++));
            rows.Add(Translation(InstrumentTranslationKeys.Interpretation, $"{abo} Neg", $"{abo}|Negative", sort++));
        }

        return rows;
    }

    /// <summary>Example meanings for the Echo's error code table; the laboratory confirms them from the vendor manual (OCD-066).</summary>
    private static readonly Dictionary<string, string> EchoErrorMeanings = new()
    {
        ["N"] = "Normal end of message (ASTM termination code)",
        ["W"] = "Reaction flagged by the instrument; graded NT by the \"Reaction error to NT\" rule"
    };

    private static InstrumentValueTranslation ErrorCode(string code, InstrumentErrorAction action, int sort, string meaning)
    {
        var row = Translation(InstrumentTranslationKeys.ErrorCode, code, action.ToString(), sort);
        row.Description = meaning;
        return row;
    }

    private static InstrumentValueTranslation Translation(
        string key, string instrument, string local, int sort, string? context = null,
        InterfaceTranslationDirection direction = InterfaceTranslationDirection.Inbound) =>
        new()
        {
            DataItemKey = key,
            InstrumentValue = instrument,
            LocalValue = local,
            Context = context,
            Direction = direction,
            SortOrder = sort
        };

    /// <summary>Seeded text rules whose work the translations now do (the last four changed nothing).</summary>
    private static readonly HashSet<string> EchoRulesNowTranslations =
    [
        "B Pos", "B Neg", "A Pos", "A Neg", "O Pos", "O Neg", "AB Pos", "AB Neg",
        "XM compatible", "XM incompatible", "DAT/Screen Positive", "DAT/Screen Negative"
    ];

    /// <summary>
    /// An upgraded database: the assay and analyte codes were copied from the old map; add the value translations once
    /// and turn off the seeded text rules they replace, only where a rule is still exactly as seeded.
    /// </summary>
    private static async Task EnsureEchoTranslationsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var echo = await context.InstrumentInterfaces
            .Include(i => i.Translations)
            .Include(i => i.Rules)
            .SingleAsync(i => i.Name == EchoInterfaceName, ct);
        if (echo.Translations.Any(t => t.DataItemKey is InstrumentTranslationKeys.Interpretation or InstrumentTranslationKeys.ErrorCode))
        {
            // Give the seeded error codes their meaning once, where nobody has written one.
            var unexplained = echo.Translations.Where(t => t.DataItemKey == InstrumentTranslationKeys.ErrorCode
                && string.IsNullOrWhiteSpace(t.Description) && EchoErrorMeanings.ContainsKey(t.InstrumentValue)).ToList();
            foreach (var row in unexplained)
            {
                row.Description = EchoErrorMeanings[row.InstrumentValue];
            }

            if (unexplained.Count > 0)
            {
                await context.SaveChangesAsync(ct);
            }

            return;
        }

        if (!echo.Translations.Any(t => t.DataItemKey == InstrumentTranslationKeys.ResultTestCode))
        {
            foreach (var row in EchoCodeTranslations())
            {
                echo.Translations.Add(row);
            }
        }

        foreach (var row in EchoValueTranslations().Where(v => !echo.Translations.Any(t =>
                     t.DataItemKey == v.DataItemKey && string.Equals(t.InstrumentValue, v.InstrumentValue, StringComparison.OrdinalIgnoreCase))))
        {
            echo.Translations.Add(row);
        }

        var seeded = EchoRules().ToDictionary(r => r.Name);
        foreach (var rule in echo.Rules.Where(r => r.IsEnabled && EchoRulesNowTranslations.Contains(r.Name)))
        {
            if (seeded.TryGetValue(rule.Name, out var original)
                && rule.ConditionExpression == original.ConditionExpression
                && rule.ActionExpression == original.ActionExpression)
            {
                rule.IsEnabled = false;
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private static List<InstrumentInterfaceRule> EchoRules() =>
    [
        Rule("Numeric 40 to 4+", "message.subtest_result >= 40 AND message.subtest_result < 80", "set('subtest_result','3+')", 10),
        Rule("Numeric 80 to 4+", "message.subtest_result >= 80", "set('subtest_result','4+')", 11),
        Rule("Numeric 20 to 2+", "message.subtest_result >= 20 AND message.subtest_result < 40", "set('subtest_result','2+')", 12),
        Rule("Numeric 1 to 1+", "message.subtest_result >= 1 AND message.subtest_result < 20", "set('subtest_result','1+')", 13),
        Rule("Numeric 0", "message.subtest_result = 0", "set('subtest_result','0')", 14),
        Rule("Reaction error to NT", "message.subtest_result IN ('?', 'C', 'Q', 'M', 'R', 'U', 'X')", "set('subtest_result','NT'); set('error','W')", 20),
        Rule("B Pos", "message.test_interp = 'B Pos'", "set('test_interp','B|Positive')", 30),
        Rule("B Neg", "message.test_interp = 'B Neg'", "set('test_interp','B|Negative')", 31),
        Rule("A Pos", "message.test_interp = 'A Pos'", "set('test_interp','A|Positive')", 32),
        Rule("A Neg", "message.test_interp = 'A Neg'", "set('test_interp','A|Negative')", 33),
        Rule("O Pos", "message.test_interp = 'O Pos'", "set('test_interp','O|Positive')", 34),
        Rule("O Neg", "message.test_interp = 'O Neg'", "set('test_interp','O|Negative')", 35),
        Rule("AB Pos", "message.test_interp = 'AB Pos'", "set('test_interp','AB|Positive')", 36),
        Rule("AB Neg", "message.test_interp = 'AB Neg'", "set('test_interp','AB|Negative')", 37),
        Rule("XM compatible", "message.test_interp = 'IgG Comp (Check ABO Comp)'", "set('test_interp','Compatible')", 40),
        Rule("XM incompatible", "message.test_interp = 'Incompatible'", "set('test_interp','Incompatible')", 41),
        Rule("DAT/Screen Positive", "message.test_interp = 'Positive'", "set('test_interp','Positive')", 50),
        Rule("DAT/Screen Negative", "message.test_interp = 'Negative'", "set('test_interp','Negative')", 51)
    ];

    private static InstrumentInterfaceRule Rule(string name, string condition, string action, int sort) =>
        new()
        {
            Name = name,
            ConditionExpression = condition,
            ActionExpression = action,
            SortOrder = sort,
            IsEnabled = true
        };
}
