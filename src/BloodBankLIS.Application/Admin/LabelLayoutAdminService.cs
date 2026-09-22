using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record LabelLayoutView(
    LabelLayoutKind Kind,
    bool IsSaved,
    LabelLayoutDocument Document,
    bool RequiresPtagId);

public sealed class LabelLayoutAdminService : ConfigAdminServiceBase
{
    private readonly IRepository<LabelLayout> _layouts;
    private readonly IPermissionEvaluator? _permissions;

    public LabelLayoutAdminService(
        IRepository<LabelLayout> layouts,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissions = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _layouts = layouts;
        _permissions = permissions;
    }

    public async Task<LabelLayoutView> GetAsync(LabelLayoutKind kind, CancellationToken ct = default)
    {
        var saved = await FindAsync(kind, ct);
        if (saved is null || string.IsNullOrWhiteSpace(saved.DocumentJson))
        {
            var defaults = Defaults(kind);
            return new LabelLayoutView(kind, false, defaults, false);
        }

        var document = Read(saved.DocumentJson) ?? Defaults(kind);
        return new LabelLayoutView(kind, true, document, kind == LabelLayoutKind.Ptag && LabelFields.RequiresPtagId(document));
    }

    public async Task<bool> RequiresPtagIdAsync(CancellationToken ct = default)
    {
        var view = await GetAsync(LabelLayoutKind.Ptag, ct);
        return view.IsSaved && view.RequiresPtagId;
    }

    public async Task<LabelLayoutDocument?> GetSavedDocumentAsync(LabelLayoutKind kind, CancellationToken ct = default)
    {
        var saved = await FindAsync(kind, ct);
        return saved is null ? null : Read(saved.DocumentJson);
    }

    public async Task<EvaluationResult<LabelLayoutView>> SaveAsync(
        LabelLayoutKind kind, LabelLayoutDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var denied = await RejectAsync<LabelLayoutView>(
            PermissionCodes.AdminConfigEdit, PrinterAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var error = Validate(kind, document);
        if (error is not null)
        {
            return EvaluationResult<LabelLayoutView>.Fail(error);
        }

        Normalize(kind, document);
        var existing = await FindAsync(kind, ct);
        var creating = existing is null;
        var entity = existing ?? new LabelLayout { Kind = kind };
        var oldJson = entity.DocumentJson;
        entity.DocumentJson = JsonSerializer.Serialize(document, LabelFields.JsonOptions);
        if (creating)
        {
            await _layouts.AddAsync(entity, ct);
        }

        await UnitOfWork.SaveChangesAsync(ct);
        var view = new LabelLayoutView(
            kind, true, document, kind == LabelLayoutKind.Ptag && LabelFields.RequiresPtagId(document));
        RecordChange(
            kind == LabelLayoutKind.Specimen ? "SpecimenLabelLayout" : "PtagLabelLayout",
            entity.Id,
            1,
            creating ? ConfigChangeAction.Create : ConfigChangeAction.Update,
            AuditEventType.Configure,
            creating ? null : oldJson,
            entity.DocumentJson,
            "Label layout saved.");
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<LabelLayoutView>.Ok(view);
    }

    private async Task<LabelLayout?> FindAsync(LabelLayoutKind kind, CancellationToken ct)
    {
        var rows = await _layouts.ListAsync(l => l.Kind == kind, ct);
        return rows.FirstOrDefault();
    }

    private async Task<EvaluationResult<T>?> RejectAsync<T>(
        string permission, Func<bool, RuleResult> rule, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(CurrentUser.UserName, permission, ct);
        var result = rule(allowed);
        return result.Severity == RuleSeverity.HardStop
            ? EvaluationResult<T>.Blocked(new RuleEvaluation([result]))
            : null;
    }

    private static LabelLayoutDocument Defaults(LabelLayoutKind kind) =>
        kind == LabelLayoutKind.Specimen ? LabelLayoutDefaults.Specimen() : LabelLayoutDefaults.Ptag();

    private static LabelLayoutDocument? Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<LabelLayoutDocument>(json, LabelFields.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Validate(LabelLayoutKind kind, LabelLayoutDocument document)
    {
        if (document.Page1.Count == 0)
        {
            return "Place at least one field on the label.";
        }

        foreach (var field in document.Page1.Concat(document.Page2))
        {
            if (!LabelFields.IsKnown(field.Key))
            {
                return $"Unknown field '{field.Key}'.";
            }

            if (field.Key == LabelFields.StaticText && string.IsNullOrWhiteSpace(field.StaticText))
            {
                return "Static text needs the words to print.";
            }
        }

        if (kind == LabelLayoutKind.Specimen && document.Page2.Count > 0)
        {
            return "A specimen label has one page.";
        }

        return null;
    }

    private static void Normalize(LabelLayoutKind kind, LabelLayoutDocument document)
    {
        var defaults = Defaults(kind);
        document.WidthDots = defaults.WidthDots;
        document.HeightDots = defaults.HeightDots;
        document.Page2WidthDots = defaults.Page2WidthDots;
        document.Page2HeightDots = defaults.Page2HeightDots;
        NormalizePage(document.Page1, document.WidthDots, document.HeightDots, allowBarcode: true);
        NormalizePage(document.Page2, document.Page2WidthDots, document.Page2HeightDots, allowBarcode: false);
        if (kind == LabelLayoutKind.Specimen)
        {
            document.Page2.Clear();
        }
    }

    private static void NormalizePage(List<PlacedLabelField> fields, int width, int height, bool allowBarcode)
    {
        foreach (var field in fields)
        {
            field.X = Math.Clamp(field.X, 0, Math.Max(0, width - 8));
            field.Y = Math.Clamp(field.Y, 0, Math.Max(0, height - 8));
            field.FontHeight = Math.Clamp(field.FontHeight, 8, 72);
            field.Barcode = allowBarcode && field.Barcode && field.Key is LabelFields.Accession or LabelFields.UnitNumber or LabelFields.PtagId;
            field.StaticText = string.IsNullOrWhiteSpace(field.StaticText) ? null : field.StaticText.Trim();
        }
    }
}
