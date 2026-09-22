using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Application.Tests;

public class VisualReportCompilerTests
{
    private static readonly IReadOnlyList<ReportField> Fields =
    [
        new() { Code = "product_name", Label = "Product" },
        new() { Code = "status", Label = "Status" },
        new() { Code = "volume", Label = "Volume", Kind = ReportValueKind.Number }
    ];

    private static readonly IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows =
    [
        Row("RBC", "Available", "300"),
        Row("RBC", "Quarantine", "250"),
        Row("FFP", "Available", "200")
    ];

    [Fact]
    public void Filter_Equals_KeepsMatchingRows()
    {
        var layout = new ReportLayout
        {
            SourceCode = "inventory_on_hand",
            Columns = ["product_name", "status", "volume"],
            Filters = [new ReportFilter { Id = "status", Field = "status", Operator = ReportFilterOperator.Equals, Value = "Available" }]
        };

        var result = VisualReportCompiler.Compile(layout, Fields, Rows, null, 100);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Rows.Count);
        Assert.All(result.Value.Rows, row => Assert.Equal("Available", row[1]));
    }

    [Fact]
    public void GroupAndCount_AggregatesEachProduct()
    {
        var layout = new ReportLayout
        {
            SourceCode = "inventory_on_hand",
            GroupBy = ["product_name"],
            Aggregates = [new ReportAggregate { Function = ReportAggregateFunction.Count, Alias = "Count" }]
        };

        var result = VisualReportCompiler.Compile(layout, Fields, Rows, null, 100);

        Assert.True(result.Succeeded);
        Assert.Equal(["product_name", "Count"], result.Value!.Columns);
        var rbc = result.Value.Rows.Single(row => row[0] == "RBC");
        var ffp = result.Value.Rows.Single(row => row[0] == "FFP");
        Assert.Equal("2", rbc[1]);
        Assert.Equal("1", ffp[1]);
    }

    [Fact]
    public void Sum_AddsNumericFieldInsideTheGroup()
    {
        var layout = new ReportLayout
        {
            SourceCode = "inventory_on_hand",
            GroupBy = ["product_name"],
            Aggregates = [new ReportAggregate { Field = "volume", Function = ReportAggregateFunction.Sum, Alias = "Volume" }],
            Filters =
            [
                new ReportFilter { Id = "status", Field = "status", Operator = ReportFilterOperator.Equals, PromptAtRun = true }
            ]
        };

        var result = VisualReportCompiler.Compile(
            layout,
            Fields,
            Rows,
            new Dictionary<string, string?> { ["status"] = "Available" },
            100);

        Assert.True(result.Succeeded);
        var rbc = result.Value!.Rows.Single(row => row[0] == "RBC");
        Assert.Equal("300", rbc[1]);
    }

    private static Dictionary<string, string?> Row(string product, string status, string volume) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["product_name"] = product,
            ["status"] = status,
            ["volume"] = volume
        };
}
