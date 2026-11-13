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

    // End-user testing pass 50: "To 2026-11-12" stopped at midnight UTC, so a one-day range returned nothing.
    [Fact]
    public void DateRange_CountsWholeLocalDays_AndShowsLocalTimes()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test-5", "Test-5");
        IReadOnlyList<ReportField> fields =
        [
            new() { Code = "occurred_utc", Label = "Occurred", Kind = ReportValueKind.Date },
            new() { Code = "event_type", Label = "Event" }
        ];
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
        [
            new Dictionary<string, string?> { ["occurred_utc"] = "2026-11-12T04:59:00Z", ["event_type"] = "late on the 11th" },
            new Dictionary<string, string?> { ["occurred_utc"] = "2026-11-12T06:52:03Z", ["event_type"] = "early on the 12th" },
            new Dictionary<string, string?> { ["occurred_utc"] = "2026-11-13T04:30:00Z", ["event_type"] = "late on the 12th" },
            new Dictionary<string, string?> { ["occurred_utc"] = "2026-11-13T05:00:00Z", ["event_type"] = "midnight on the 13th" },
            new Dictionary<string, string?> { ["occurred_utc"] = null, ["event_type"] = "no date" }
        ];
        var layout = new ReportLayout
        {
            SourceCode = "crossmatch_transfusion",
            Columns = ["occurred_utc", "event_type"],
            Filters = [new ReportFilter { Id = "range", Field = "occurred_utc", Operator = ReportFilterOperator.Between, PromptAtRun = true }]
        };
        var parameters = new Dictionary<string, string?> { ["range"] = "2026-11-12", ["range:to"] = "2026-11-12" };

        var result = VisualReportCompiler.Compile(layout, fields, rows, parameters, 100, zone);

        Assert.True(result.Succeeded);
        Assert.Equal(["occurred_utc", "event_type"], result.Value!.Columns);
        Assert.Equal(["Occurred", "Event"], result.Value.ColumnLabels);
        Assert.Equal(["early on the 12th", "late on the 12th"], result.Value.Rows.Select(r => r[1]));
        Assert.Equal("2026-11-12 01:52", result.Value.Rows[0][0]);
        Assert.True(VisualReportCompiler.MatchDate(ReportFilterOperator.After, "2026-11-13T05:00:00Z", "2026-11-12", null, zone));
        Assert.False(VisualReportCompiler.MatchDate(ReportFilterOperator.After, "2026-11-13T04:30:00Z", "2026-11-12", null, zone));
        Assert.True(VisualReportCompiler.MatchDate(ReportFilterOperator.Before, "2026-11-12T04:59:00Z", "2026-11-12", null, zone));
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
