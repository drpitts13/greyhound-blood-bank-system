using System.Globalization;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Application.Reporting;

/// <summary>
/// Applies a visual layout to rows already projected from a report source.
/// Filtering, grouping, and aggregation run in memory so SQLite and SQL Server match.
/// </summary>
public static class VisualReportCompiler
{
    public static OperationResult<ReportGrid> Compile(
        ReportLayout layout,
        IReadOnlyList<ReportField> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        IReadOnlyDictionary<string, string?>? parameters,
        int maxRows)
    {
        var fieldByCode = fields.ToDictionary(f => f.Code, StringComparer.OrdinalIgnoreCase);
        var validation = Validate(layout, fieldByCode);
        if (validation is not null)
            return OperationResult<ReportGrid>.Fail(validation);

        var filtered = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var row in rows)
        {
            if (Matches(row, layout.Filters, fieldByCode, parameters))
                filtered.Add(row);
        }

        var grouped = layout.GroupBy.Count > 0 || layout.Aggregates.Count > 0;
        List<string> columns;
        List<IReadOnlyList<string?>> output;

        if (!grouped)
        {
            columns = layout.Columns.ToList();
            output = filtered
                .Select(row => (IReadOnlyList<string?>)columns.Select(column => Value(row, column)).ToList())
                .ToList();
        }
        else
        {
            columns = layout.GroupBy.Concat(layout.Aggregates.Select(OutputName)).ToList();
            output = filtered
                .GroupBy(row => string.Join("\u001f", layout.GroupBy.Select(column => Value(row, column) ?? string.Empty)), StringComparer.Ordinal)
                .Select(group =>
                {
                    var cells = new List<string?>(columns.Count);
                    foreach (var column in layout.GroupBy)
                        cells.Add(Value(group.First(), column));
                    foreach (var aggregate in layout.Aggregates)
                        cells.Add(Aggregate(aggregate, group, fieldByCode));
                    return (IReadOnlyList<string?>)cells;
                })
                .ToList();
        }

        output = Sort(output, columns, layout.Sorts);
        var truncated = output.Count > maxRows;
        if (truncated)
            output = output.Take(maxRows).ToList();

        return OperationResult<ReportGrid>.Ok(new ReportGrid
        {
            Columns = columns,
            Rows = output,
            Truncated = truncated
        });
    }

    public static string? Validate(ReportLayout layout, IReadOnlyDictionary<string, ReportField> fields)
    {
        if (string.IsNullOrWhiteSpace(layout.SourceCode))
            return "Choose a data source.";

        if (layout.Columns.Count == 0 && layout.Aggregates.Count == 0)
            return "Add at least one column or aggregate.";

        foreach (var column in layout.Columns.Concat(layout.GroupBy).Concat(layout.Filters.Select(f => f.Field)))
        {
            if (!fields.ContainsKey(column))
                return $"Unknown field '{column}'.";
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in layout.GroupBy)
        {
            if (!names.Add(column))
                return $"'{column}' is listed more than once.";
        }

        foreach (var aggregate in layout.Aggregates)
        {
            if (aggregate.Function != ReportAggregateFunction.Count && string.IsNullOrWhiteSpace(aggregate.Field))
                return "That aggregate needs a field.";

            if (!string.IsNullOrWhiteSpace(aggregate.Field) && !fields.ContainsKey(aggregate.Field))
                return $"Unknown field '{aggregate.Field}'.";

            if (aggregate.Function == ReportAggregateFunction.Sum
                && aggregate.Field is not null
                && fields.TryGetValue(aggregate.Field, out var field)
                && field.Kind != ReportValueKind.Number)
            {
                return $"Sum requires a numeric field. '{field.Label}' is not numeric.";
            }

            var name = OutputName(aggregate);
            if (!names.Add(name))
                return $"'{name}' is listed more than once.";
        }

        foreach (var sort in layout.Sorts)
        {
            if (!names.Contains(sort.Field) && !layout.Columns.Contains(sort.Field, StringComparer.OrdinalIgnoreCase))
                return $"Cannot sort by unknown column '{sort.Field}'.";
        }

        return null;
    }

    public static string OutputName(ReportAggregate aggregate)
    {
        if (!string.IsNullOrWhiteSpace(aggregate.Alias))
            return aggregate.Alias.Trim();

        return aggregate.Function switch
        {
            ReportAggregateFunction.Count => "Count",
            ReportAggregateFunction.CountDistinct => $"Count distinct {aggregate.Field}",
            ReportAggregateFunction.Sum => $"Sum of {aggregate.Field}",
            ReportAggregateFunction.Min => $"Min of {aggregate.Field}",
            ReportAggregateFunction.Max => $"Max of {aggregate.Field}",
            _ => aggregate.Function.ToString()
        };
    }

    private static bool Matches(
        IReadOnlyDictionary<string, string?> row,
        IReadOnlyList<ReportFilter> filters,
        IReadOnlyDictionary<string, ReportField> fields,
        IReadOnlyDictionary<string, string?>? parameters)
    {
        foreach (var filter in filters)
        {
            var value = filter.Value;
            var valueTo = filter.ValueTo;
            if (filter.PromptAtRun && parameters is not null)
            {
                if (parameters.TryGetValue(filter.Id, out var prompted))
                    value = prompted;
                if (parameters.TryGetValue(filter.Id + ":to", out var promptedTo))
                    valueTo = promptedTo;
            }

            if (filter.PromptAtRun && string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(valueTo))
                continue;

            if (!fields.TryGetValue(filter.Field, out var field))
                return false;

            var cell = Value(row, filter.Field);
            if (!MatchOne(field.Kind, filter.Operator, cell, value, valueTo))
                return false;
        }

        return true;
    }

    private static bool MatchOne(ReportValueKind kind, ReportFilterOperator op, string? cell, string? value, string? valueTo)
    {
        switch (op)
        {
            case ReportFilterOperator.Equals:
                return string.Equals(cell ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            case ReportFilterOperator.NotEquals:
                return !string.Equals(cell ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            case ReportFilterOperator.Contains:
                return (cell ?? string.Empty).Contains(value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            case ReportFilterOperator.Before:
                return Compare(kind, cell, value) < 0;
            case ReportFilterOperator.After:
                return Compare(kind, cell, value) > 0;
            case ReportFilterOperator.Between:
                return Compare(kind, cell, value) >= 0 && Compare(kind, cell, valueTo) <= 0;
            default:
                return false;
        }
    }

    private static int Compare(ReportValueKind kind, string? left, string? right)
    {
        if (kind == ReportValueKind.Number
            && decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNumber)
            && decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (kind == ReportValueKind.Date
            && TryParseDate(left, out var leftDate)
            && TryParseDate(right, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Aggregate(
        ReportAggregate aggregate,
        IEnumerable<IReadOnlyDictionary<string, string?>> rows,
        IReadOnlyDictionary<string, ReportField> fields)
    {
        var list = rows.ToList();
        switch (aggregate.Function)
        {
            case ReportAggregateFunction.Count:
                return list.Count.ToString(CultureInfo.InvariantCulture);
            case ReportAggregateFunction.CountDistinct:
                return list.Select(row => Value(row, aggregate.Field)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    .ToString(CultureInfo.InvariantCulture);
            case ReportAggregateFunction.Sum:
                var sum = list.Select(row => Value(row, aggregate.Field))
                    .Select(value => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : 0m)
                    .Sum();
                return sum.ToString("0.##", CultureInfo.InvariantCulture);
            case ReportAggregateFunction.Min:
            case ReportAggregateFunction.Max:
                var kind = aggregate.Field is not null && fields.TryGetValue(aggregate.Field, out var field)
                    ? field.Kind
                    : ReportValueKind.Text;
                var values = list.Select(row => Value(row, aggregate.Field)).Where(value => value is not null).Cast<string>().ToList();
                if (values.Count == 0)
                    return null;
                values.Sort((a, b) => Compare(kind, a, b));
                return aggregate.Function == ReportAggregateFunction.Min ? values[0] : values[^1];
            default:
                return null;
        }
    }

    private static List<IReadOnlyList<string?>> Sort(
        List<IReadOnlyList<string?>> rows,
        IReadOnlyList<string> columns,
        IReadOnlyList<ReportSort> sorts)
    {
        if (sorts.Count == 0)
            return rows;

        IOrderedEnumerable<IReadOnlyList<string?>>? ordered = null;
        foreach (var sort in sorts)
        {
            var index = IndexOf(columns, sort.Field);
            if (index < 0)
                continue;

            ordered = ordered is null
                ? sort.Direction == ReportSortDirection.Descending
                    ? rows.OrderByDescending(row => row[index], StringComparer.OrdinalIgnoreCase)
                    : rows.OrderBy(row => row[index], StringComparer.OrdinalIgnoreCase)
                : sort.Direction == ReportSortDirection.Descending
                    ? ordered.ThenByDescending(row => row[index], StringComparer.OrdinalIgnoreCase)
                    : ordered.ThenBy(row => row[index], StringComparer.OrdinalIgnoreCase);
        }

        return ordered?.ToList() ?? rows;
    }

    private static int IndexOf(IReadOnlyList<string> columns, string field)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i], field, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string? Value(IReadOnlyDictionary<string, string?> row, string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return null;

        foreach (var pair in row)
        {
            if (string.Equals(pair.Key, field, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return null;
    }

    private static bool TryParseDate(string? text, out DateTime value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }
}
