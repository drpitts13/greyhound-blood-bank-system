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
            return;
        }

        var iface = new InstrumentInterface
        {
            Name = EchoInterfaceName,
            IsEnabled = true,
            Environment = "Development",
            Version = 1,
            Connection = new InstrumentConnection { Port = EchoListenPort },
            FieldMappings = EchoFieldMappings(),
            TestMappings = EchoTestMappings(),
            Rules = EchoRules()
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

    private static List<InstrumentTestMapping> EchoTestMappings()
    {
        var rows = new List<InstrumentTestMapping>();
        var order = 0;

        void Assay(string instrument, string application, int sort, InterfaceTranslationDirection dir = InterfaceTranslationDirection.Both) =>
            rows.Add(new InstrumentTestMapping
            {
                InstrumentCode = instrument,
                ApplicationCode = application,
                Kind = InstrumentMappingKind.Assay,
                Direction = dir,
                SortOrder = sort
            });

        void Analyte(string instrument, string application, int sort) =>
            rows.Add(new InstrumentTestMapping
            {
                InstrumentCode = instrument,
                ApplicationCode = application,
                Kind = InstrumentMappingKind.Analyte,
                Direction = InterfaceTranslationDirection.Inbound,
                SortOrder = sort
            });

        Assay("Group", "ABORH", order++);
        Assay("Group2", "ABORH", order++, InterfaceTranslationDirection.Inbound);
        Assay("Pediatric", "ABORH", order++, InterfaceTranslationDirection.Inbound);
        Assay("Confirm", "ABORH", order++, InterfaceTranslationDirection.Inbound);
        Assay("Donor", "ABORH-RETYPE", order++);
        Assay("Screen", "ABSC", order++);
        Assay("Crossmatch", "XM", order++);
        Assay("DAT", "DAT", order++);
        Assay("Weak D", "ABORH", order++, InterfaceTranslationDirection.Inbound);
        Assay("Ready ID", "ABID", order++);

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
