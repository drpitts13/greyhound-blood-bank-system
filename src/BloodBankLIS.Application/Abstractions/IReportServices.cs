using BloodBankLIS.Application.Reporting;

namespace BloodBankLIS.Application.Abstractions;

public interface IReportSourceReader
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> LoadAsync(string sourceCode, CancellationToken cancellationToken = default);
}

public interface IReportSqlExecutor
{
    Task<ReportGrid> QueryAsync(string sql, int maxRows, CancellationToken cancellationToken = default);
}

public interface IReportDocumentRenderer
{
    byte[] ToPdf(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string?>> rows, bool truncated);

    byte[] ToExcel(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string?>> rows, bool truncated);
}
