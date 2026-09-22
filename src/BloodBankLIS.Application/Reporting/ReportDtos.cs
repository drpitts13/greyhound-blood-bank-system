using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Application.Reporting;

public static class ReportLimits
{
    public const int PreviewRows = 500;
    public const int ExportRows = 50_000;
    public const int SqlTimeoutSeconds = 30;
}

public sealed class ReportGrid
{
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();

    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; init; } = Array.Empty<IReadOnlyList<string?>>();

    public bool Truncated { get; init; }
}

public sealed class ReportSummaryDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ReportKind Kind { get; init; }
    public bool IsSystemOwned { get; init; }
}

public sealed class ReportDetailDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ReportKind Kind { get; init; }
    public bool IsSystemOwned { get; init; }
    public ReportLayout? Layout { get; init; }
    public string? SqlText { get; init; }
    public IReadOnlyList<ReportPromptDto> Prompts { get; init; } = Array.Empty<ReportPromptDto>();
}

public sealed class ReportPromptDto
{
    public string Id { get; init; } = string.Empty;
    public string Field { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public ReportFilterOperator Operator { get; init; }
    public ReportValueKind ValueKind { get; init; }
}

public sealed class ReportResultDto
{
    public long? ReportId { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; init; } = Array.Empty<IReadOnlyList<string?>>();
    public bool Truncated { get; init; }
}

public sealed class SaveReportRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReportKind Kind { get; set; } = ReportKind.Visual;
    public ReportLayout? Layout { get; set; }
    public string? SqlText { get; set; }
}

public sealed class PreviewReportRequest
{
    public string? Name { get; set; }
    public ReportKind Kind { get; set; } = ReportKind.Visual;
    public ReportLayout? Layout { get; set; }
    public string? SqlText { get; set; }
    public Dictionary<string, string?>? Parameters { get; set; }
}

public sealed class RunReportRequest
{
    public Dictionary<string, string?>? Parameters { get; set; }
}

public sealed class ReportFile
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public byte[] Bytes { get; init; } = Array.Empty<byte>();
}
