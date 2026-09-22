using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Reporting;

public sealed class ReportService
{
    private readonly IReportDefinitionStore _store;
    private readonly IReportSourceReader _sources;
    private readonly IReportSqlExecutor _sql;
    private readonly IReportDocumentRenderer _renderer;
    private readonly IAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionEvaluator _permissions;

    public ReportService(
        IReportDefinitionStore store,
        IReportSourceReader sources,
        IReportSqlExecutor sql,
        IReportDocumentRenderer renderer,
        IAuditWriter audit,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IPermissionEvaluator permissions)
    {
        _store = store;
        _sources = sources;
        _sql = sql;
        _renderer = renderer;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<OperationResult<IReadOnlyList<ReportSummaryDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanRunAsync(cancellationToken))
            return OperationResult<IReadOnlyList<ReportSummaryDto>>.Fail("Running reports requires report.run or report.design.");

        var rows = await _store.ListAsync(cancellationToken);
        return OperationResult<IReadOnlyList<ReportSummaryDto>>.Ok(rows
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToSummary)
            .ToList());
    }

    public async Task<OperationResult<ReportDetailDto>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!await CanRunAsync(cancellationToken))
            return OperationResult<ReportDetailDto>.Fail("Running reports requires report.run or report.design.");

        var entity = await _store.GetAsync(id, cancellationToken);
        return entity is null
            ? OperationResult<ReportDetailDto>.Fail("Report not found.")
            : OperationResult<ReportDetailDto>.Ok(ToDetail(entity));
    }

    public IReadOnlyList<ReportSourceDefinition> Sources() => ReportSourceCatalog.All;

    public RuleResult ValidateSql(string? sql) => ReportSqlGuard.Evaluate(sql);

    public async Task<OperationResult<ReportDetailDto>> SaveAsync(long? id, SaveReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanDesignAsync(cancellationToken))
            return OperationResult<ReportDetailDto>.Fail("Designing reports requires report.design.");

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (request.Name ?? string.Empty).Trim();
        if (code.Length is < 2 or > 40 || !code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            return OperationResult<ReportDetailDto>.Fail("Code must be 2–40 letters, digits, or hyphens.");
        if (name.Length is < 1 or > 200)
            return OperationResult<ReportDetailDto>.Fail("Name is required.");

        var definitionError = ValidateDefinition(request.Kind, request.Layout, request.SqlText);
        if (definitionError is not null)
            return OperationResult<ReportDetailDto>.Fail(definitionError);

        if (await _store.CodeExistsAsync(code, id, cancellationToken))
            return OperationResult<ReportDetailDto>.Fail($"Report code '{code}' is already in use.");

        ReportDefinition entity;
        if (id is null)
        {
            entity = new ReportDefinition();
            await _store.AddAsync(entity, cancellationToken);
        }
        else
        {
            var existing = await _store.GetAsync(id.Value, cancellationToken);
            if (existing is null)
                return OperationResult<ReportDetailDto>.Fail("Report not found.");
            if (existing.IsSystemOwned)
                return OperationResult<ReportDetailDto>.Fail("System reports cannot be edited. Clone the report to change it.");
            entity = existing;
        }

        entity.Code = code;
        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Kind = request.Kind;
        entity.IsSystemOwned = false;
        entity.LayoutJson = request.Kind == ReportKind.Visual && request.Layout is not null
            ? ReportLayoutJson.Serialize(request.Layout)
            : null;
        entity.SqlText = request.Kind == ReportKind.Sql ? request.SqlText!.Trim() : null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return OperationResult<ReportDetailDto>.Ok(ToDetail(entity));
    }

    public async Task<OperationResult<ReportDetailDto>> CloneAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!await CanDesignAsync(cancellationToken))
            return OperationResult<ReportDetailDto>.Fail("Designing reports requires report.design.");

        var source = await _store.GetAsync(id, cancellationToken);
        if (source is null)
            return OperationResult<ReportDetailDto>.Fail("Report not found.");

        var code = await NextCopyCodeAsync(source.Code, cancellationToken);
        var copy = new ReportDefinition
        {
            Code = code,
            Name = source.Name + " copy",
            Description = source.Description,
            Kind = source.Kind,
            IsSystemOwned = false,
            LayoutJson = source.LayoutJson,
            SqlText = source.SqlText
        };
        await _store.AddAsync(copy, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return OperationResult<ReportDetailDto>.Ok(ToDetail(copy));
    }

    public async Task<OperationResult<bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!await CanDesignAsync(cancellationToken))
            return OperationResult<bool>.Fail("Designing reports requires report.design.");

        var entity = await _store.GetAsync(id, cancellationToken);
        if (entity is null)
            return OperationResult<bool>.Fail("Report not found.");
        if (entity.IsSystemOwned)
            return OperationResult<bool>.Fail("System reports cannot be deleted.");

        _store.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<ReportResultDto>> RunAsync(
        long id,
        IReadOnlyDictionary<string, string?>? parameters,
        CancellationToken cancellationToken = default)
    {
        if (!await CanRunAsync(cancellationToken))
            return OperationResult<ReportResultDto>.Fail("Running reports requires report.run or report.design.");

        var entity = await _store.GetAsync(id, cancellationToken);
        if (entity is null)
            return OperationResult<ReportResultDto>.Fail("Report not found.");

        var compiled = await CompileAsync(entity.Kind, entity.Name, DeserializeLayout(entity), entity.SqlText, parameters, ReportLimits.PreviewRows, cancellationToken);
        if (!compiled.Succeeded)
            return OperationResult<ReportResultDto>.Fail(compiled.Error!);

        await AuditAsync(AuditEventType.ReportRun, entity.Id, entity.Code, "run", compiled.Value!, cancellationToken);
        return OperationResult<ReportResultDto>.Ok(ToResult(entity.Id, entity.Name, compiled.Value!));
    }

    public async Task<OperationResult<ReportResultDto>> PreviewAsync(PreviewReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanDesignAsync(cancellationToken))
            return OperationResult<ReportResultDto>.Fail("Designing reports requires report.design.");

        var compiled = await CompileAsync(request.Kind, request.Name ?? "Preview", request.Layout, request.SqlText, request.Parameters, ReportLimits.PreviewRows, cancellationToken);
        if (!compiled.Succeeded)
            return OperationResult<ReportResultDto>.Fail(compiled.Error!);

        await AuditAsync(AuditEventType.ReportRun, null, "ad-hoc", "preview", compiled.Value!, cancellationToken);
        return OperationResult<ReportResultDto>.Ok(ToResult(null, request.Name ?? "Preview", compiled.Value!));
    }

    public async Task<OperationResult<ReportFile>> ExportAsync(
        long id,
        string format,
        IReadOnlyDictionary<string, string?>? parameters,
        CancellationToken cancellationToken = default)
    {
        if (!await CanRunAsync(cancellationToken))
            return OperationResult<ReportFile>.Fail("Running reports requires report.run or report.design.");

        var entity = await _store.GetAsync(id, cancellationToken);
        if (entity is null)
            return OperationResult<ReportFile>.Fail("Report not found.");

        var compiled = await CompileAsync(entity.Kind, entity.Name, DeserializeLayout(entity), entity.SqlText, parameters, ReportLimits.ExportRows, cancellationToken);
        if (!compiled.Succeeded)
            return OperationResult<ReportFile>.Fail(compiled.Error!);

        var grid = compiled.Value!;
        var pdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);
        var excel = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase);
        if (!pdf && !excel)
            return OperationResult<ReportFile>.Fail("Export format must be pdf or xlsx.");

        byte[] bytes;
        string contentType;
        string extension;
        try
        {
            if (pdf)
            {
                bytes = _renderer.ToPdf(entity.Name, grid.Columns, grid.Rows, grid.Truncated);
                contentType = "application/pdf";
                extension = "pdf";
            }
            else
            {
                bytes = _renderer.ToExcel(entity.Name, grid.Columns, grid.Rows, grid.Truncated);
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                extension = "xlsx";
            }
        }
        catch (Exception ex)
        {
            return OperationResult<ReportFile>.Fail("The report could not be exported. " + ex.Message);
        }

        await AuditAsync(AuditEventType.Export, entity.Id, entity.Code, extension, grid, cancellationToken);
        return OperationResult<ReportFile>.Ok(new ReportFile
        {
            FileName = entity.Code + "." + extension,
            ContentType = contentType,
            Bytes = bytes
        });
    }

    private async Task<OperationResult<ReportGrid>> CompileAsync(
        ReportKind kind,
        string name,
        ReportLayout? layout,
        string? sql,
        IReadOnlyDictionary<string, string?>? parameters,
        int maxRows,
        CancellationToken cancellationToken)
    {
        _ = name;
        if (kind == ReportKind.Sql)
        {
            var guard = ReportSqlGuard.Evaluate(sql);
            if (guard.Severity == RuleSeverity.HardStop)
                return OperationResult<ReportGrid>.Fail(guard.Message);

            try
            {
                return OperationResult<ReportGrid>.Ok(await _sql.QueryAsync(sql!.Trim().TrimEnd(';'), maxRows, cancellationToken));
            }
            catch (Exception ex)
            {
                return OperationResult<ReportGrid>.Fail("The SQL report failed. " + ex.Message);
            }
        }

        if (layout is null)
            return OperationResult<ReportGrid>.Fail("This visual report has no layout.");

        var source = ReportSourceCatalog.Find(layout.SourceCode);
        if (source is null)
            return OperationResult<ReportGrid>.Fail($"Unknown data source '{layout.SourceCode}'.");

        var fields = source.Fields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
        var layoutError = VisualReportCompiler.Validate(layout, fields);
        if (layoutError is not null)
            return OperationResult<ReportGrid>.Fail(layoutError);

        var rows = await _sources.LoadAsync(source.Code, cancellationToken);
        return VisualReportCompiler.Compile(layout, source.Fields, rows, parameters, maxRows);
    }

    private static string? ValidateDefinition(ReportKind kind, ReportLayout? layout, string? sql)
    {
        if (kind == ReportKind.Sql)
        {
            var guard = ReportSqlGuard.Evaluate(sql);
            return guard.Severity == RuleSeverity.HardStop ? guard.Message : null;
        }

        if (layout is null)
            return "A visual report needs a layout.";

        var source = ReportSourceCatalog.Find(layout.SourceCode);
        if (source is null)
            return $"Unknown data source '{layout.SourceCode}'.";

        return VisualReportCompiler.Validate(layout, source.Fields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase));
    }

    private async Task AuditAsync(AuditEventType eventType, long? id, string code, string action, ReportGrid grid, CancellationToken cancellationToken)
    {
        _audit.Record(eventType, nameof(ReportDefinition), id, newValue: new
        {
            code,
            action,
            rows = grid.Rows.Count,
            truncated = grid.Truncated,
            actor = _currentUser.UserName
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> NextCopyCodeAsync(string code, CancellationToken cancellationToken)
    {
        var stem = code.Length > 34 ? code[..34] : code;
        var candidate = stem + "-COPY";
        var n = 2;
        while (await _store.CodeExistsAsync(candidate, null, cancellationToken))
        {
            candidate = stem + "-COPY" + n.ToString();
            n++;
        }

        return candidate;
    }

    private async Task<bool> CanRunAsync(CancellationToken cancellationToken) =>
        await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.ReportRun, cancellationToken)
        || await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.ReportDesign, cancellationToken);

    private Task<bool> CanDesignAsync(CancellationToken cancellationToken) =>
        _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.ReportDesign, cancellationToken);

    private static ReportLayout? DeserializeLayout(ReportDefinition entity) =>
        entity.Kind == ReportKind.Visual ? ReportLayoutJson.Deserialize(entity.LayoutJson) : null;

    private static ReportSummaryDto ToSummary(ReportDefinition entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        Kind = entity.Kind,
        IsSystemOwned = entity.IsSystemOwned
    };

    private static ReportDetailDto ToDetail(ReportDefinition entity)
    {
        var layout = DeserializeLayout(entity);
        var prompts = new List<ReportPromptDto>();
        if (layout is not null)
        {
            var source = ReportSourceCatalog.Find(layout.SourceCode);
            foreach (var filter in layout.Filters.Where(filter => filter.PromptAtRun))
            {
                var field = source?.Fields.FirstOrDefault(item => string.Equals(item.Code, filter.Field, StringComparison.OrdinalIgnoreCase));
                prompts.Add(new ReportPromptDto
                {
                    Id = filter.Id,
                    Field = filter.Field,
                    Label = field?.Label ?? filter.Field,
                    Operator = filter.Operator,
                    ValueKind = field?.Kind ?? ReportValueKind.Text
                });
            }
        }

        return new ReportDetailDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            Kind = entity.Kind,
            IsSystemOwned = entity.IsSystemOwned,
            Layout = layout,
            SqlText = entity.SqlText,
            Prompts = prompts
        };
    }

    private static ReportResultDto ToResult(long? id, string name, ReportGrid grid) => new()
    {
        ReportId = id,
        Name = name,
        Columns = grid.Columns,
        Rows = grid.Rows,
        Truncated = grid.Truncated
    };
}
