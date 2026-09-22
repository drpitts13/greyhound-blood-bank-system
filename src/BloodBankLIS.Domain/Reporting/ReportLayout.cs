namespace BloodBankLIS.Domain.Reporting;

public enum ReportKind
{
    Visual = 0,
    Sql = 1
}

public enum ReportValueKind
{
    Text = 0,
    Number = 1,
    Date = 2
}

public enum ReportFilterOperator
{
    Equals = 0,
    NotEquals = 1,
    Contains = 2,
    Before = 3,
    After = 4,
    Between = 5
}

public enum ReportAggregateFunction
{
    Count = 0,
    CountDistinct = 1,
    Sum = 2,
    Min = 3,
    Max = 4
}

public enum ReportSortDirection
{
    Ascending = 0,
    Descending = 1
}

/// <summary>A column exposed by a visual report source and its matching reporting view.</summary>
public sealed class ReportField
{
    public required string Code { get; init; }

    public required string Label { get; init; }

    public ReportValueKind Kind { get; init; } = ReportValueKind.Text;
}

/// <summary>
/// Drag-and-drop layout. Filters marked <see cref="ReportFilter.PromptAtRun"/>
/// take their values from the run request; a blank prompt does not filter.
/// </summary>
public sealed class ReportLayout
{
    public string SourceCode { get; set; } = string.Empty;

    public List<string> Columns { get; set; } = new();

    public List<ReportFilter> Filters { get; set; } = new();

    public List<string> GroupBy { get; set; } = new();

    public List<ReportAggregate> Aggregates { get; set; } = new();

    public List<ReportSort> Sorts { get; set; } = new();
}

public sealed class ReportFilter
{
    public string Id { get; set; } = string.Empty;

    public string Field { get; set; } = string.Empty;

    public ReportFilterOperator Operator { get; set; } = ReportFilterOperator.Equals;

    public string? Value { get; set; }

    public string? ValueTo { get; set; }

    public bool PromptAtRun { get; set; }
}

public sealed class ReportAggregate
{
    public string? Field { get; set; }

    public ReportAggregateFunction Function { get; set; } = ReportAggregateFunction.Count;

    public string? Alias { get; set; }
}

public sealed class ReportSort
{
    public string Field { get; set; } = string.Empty;

    public ReportSortDirection Direction { get; set; } = ReportSortDirection.Ascending;
}
