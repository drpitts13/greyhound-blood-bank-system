using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record SpecimenLabelDesignListItemDto(
    long Id,
    string Code,
    string Name,
    SpecimenLabelPurpose Purpose,
    decimal WidthInches,
    decimal HeightInches,
    bool IsDefault,
    bool IsActive,
    int Version,
    int ElementCount);

public sealed record SpecimenLabelDesignDto(
    long Id,
    string Code,
    string Name,
    SpecimenLabelPurpose Purpose,
    int WidthDots,
    int HeightDots,
    bool IsDefault,
    bool IsActive,
    int Version,
    LabelLayoutDocument Document,
    IReadOnlyList<string> Warnings);

public sealed record CreateSpecimenLabelDesignRequest(
    string Code,
    string Name,
    SpecimenLabelPurpose Purpose,
    decimal WidthInches,
    decimal HeightInches,
    long? CopyFromId = null);

public sealed record SaveSpecimenLabelDesignRequest(string Name, LabelLayoutDocument Document);

/// <summary>
/// Specimen label designs (Cycle 657): named layouts for the tube and for aliquots, each its own size. Saving checks the
/// design (the specimen ID barcode is required; fewer than two patient identifiers warns, OCD-080), and every change is
/// audited with a configuration-history snapshot. One active design per purpose is the default used when printing
/// without a choice; the default cannot be deactivated until another takes its place.
/// </summary>
public sealed class SpecimenLabelDesignAdminService : ConfigAdminServiceBase
{
    private const string EntityName = "SpecimenLabelDesign";
    private readonly IRepository<SpecimenLabelDesign> _designs;
    private readonly IPermissionEvaluator? _permissions;

    public SpecimenLabelDesignAdminService(
        IRepository<SpecimenLabelDesign> designs,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissions = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _designs = designs;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<SpecimenLabelDesignListItemDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default) =>
        (await _designs.ListAsync(ct))
            .Where(d => !activeOnly || d.IsActive)
            .OrderBy(d => d.Purpose)
            .ThenByDescending(d => d.IsDefault)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new SpecimenLabelDesignListItemDto(
                d.Id, d.Code, d.Name, d.Purpose,
                SpecimenLabelDesignRules.ToInches(d.WidthDots), SpecimenLabelDesignRules.ToInches(d.HeightDots),
                d.IsDefault, d.IsActive, d.Version, Read(d)?.Page1.Count ?? 0))
            .ToList();

    public async Task<SpecimenLabelDesignDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var design = await _designs.GetByIdAsync(id, ct);
        return design is null ? null : ToDto(design);
    }

    /// <summary>The design a specimen label prints with: the named one if active, otherwise the purpose's default.</summary>
    public async Task<SpecimenLabelDesign?> ResolveAsync(string? code, SpecimenLabelPurpose purpose, CancellationToken ct = default)
    {
        var designs = await _designs.ListAsync(d => d.IsActive, ct);
        if (!string.IsNullOrWhiteSpace(code))
        {
            return designs.FirstOrDefault(d => string.Equals(d.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return designs.FirstOrDefault(d => d.Purpose == purpose && d.IsDefault);
    }

    public async Task<EvaluationResult<SpecimenLabelDesignDto>> CreateAsync(CreateSpecimenLabelDesignRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (request.Name ?? string.Empty).Trim();
        if (code.Length is 0 or > 30 || code.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_'))
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail("Enter a code of up to 30 letters, digits, '-' or '_'.");
        }

        if (name.Length is 0 or > 100)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail("Enter a name of up to 100 characters.");
        }

        if (SpecimenLabelDesignRules.SizeError(request.WidthInches, request.HeightInches) is { } sizeError)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail(sizeError);
        }

        var all = await _designs.ListAsync(ct);
        if (all.Any(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail($"A label design with code {code} already exists.");
        }

        var width = SpecimenLabelDesignRules.ToDots(request.WidthInches);
        var height = SpecimenLabelDesignRules.ToDots(request.HeightInches);
        LabelLayoutDocument document;
        if (request.CopyFromId is long sourceId)
        {
            var source = all.FirstOrDefault(d => d.Id == sourceId);
            if (source is null || Read(source) is not { } copied)
            {
                return EvaluationResult<SpecimenLabelDesignDto>.Fail("The design to copy was not found.");
            }

            document = copied;
            document.WidthDots = width;
            document.HeightDots = height;
            if (request.Purpose == SpecimenLabelPurpose.Tube)
            {
                document.Page1.RemoveAll(f => f.Key == LabelFields.AliquotNumber);
            }

            SpecimenLabelDesignRules.Normalize(width, height, document.Page1);
        }
        else
        {
            document = SpecimenLabelDesignRules.Starter(request.Purpose, width, height);
        }

        var design = new SpecimenLabelDesign
        {
            Code = code,
            Name = name,
            Purpose = request.Purpose,
            WidthDots = width,
            HeightDots = height,
            DocumentJson = Write(document),
            // The first design for a purpose prints by default.
            IsDefault = !all.Any(d => d.Purpose == request.Purpose && d.IsDefault),
            IsActive = true
        };
        await _designs.AddAsync(design, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        RecordChange(EntityName, design.Id, design.Version, ConfigChangeAction.Create, AuditEventType.Create,
            null, Snapshot(design), request.CopyFromId is long from ? $"Specimen label design created from design {from}." : "Specimen label design created.");
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<SpecimenLabelDesignDto>.Ok(ToDto(design));
    }

    public async Task<EvaluationResult<SpecimenLabelDesignDto>> SaveAsync(long id, SaveSpecimenLabelDesignRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        var denied = await RejectAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var design = await _designs.GetByIdAsync(id, ct);
        if (design is null)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail("Label design not found.");
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 100)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail("Enter a name of up to 100 characters.");
        }

        var document = request.Document;
        document.WidthDots = design.WidthDots;
        document.HeightDots = design.HeightDots;
        document.Page2.Clear();
        SpecimenLabelDesignRules.Normalize(design.WidthDots, design.HeightDots, document.Page1);
        var check = SpecimenLabelDesignRules.Check(design.Purpose, design.WidthDots, design.HeightDots, document.Page1);
        if (!check.Ok)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Blocked(new RuleEvaluation(
                check.Errors.Select(e => RuleResult.HardStop("LABEL-DESIGN", e)).ToList()));
        }

        var before = Snapshot(design);
        design.Name = name;
        design.DocumentJson = Write(document);
        design.Version++;
        _designs.Update(design);
        RecordChange(EntityName, design.Id, design.Version, ConfigChangeAction.Update, AuditEventType.Update,
            before, Snapshot(design), "Specimen label design saved.");
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<SpecimenLabelDesignDto>.Ok(ToDto(design),
            new RuleEvaluation(check.Warnings.Select(w => RuleResult.Warning("LABEL-DESIGN", w)).ToList()));
    }

    public async Task<EvaluationResult<SpecimenLabelDesignDto>> MakeDefaultAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var design = await _designs.GetByIdAsync(id, ct);
        if (design is null)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail("Label design not found.");
        }

        if (!design.IsActive)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail("Activate the design before making it the default.");
        }

        foreach (var listed in await _designs.ListAsync(d => d.Purpose == design.Purpose && d.IsDefault && d.Id != design.Id, ct))
        {
            // The tracked row, so the update does not meet another copy of the same design.
            if (await _designs.GetByIdAsync(listed.Id, ct) is not { } other)
            {
                continue;
            }

            other.IsDefault = false;
            _designs.Update(other);
            RecordChange(EntityName, other.Id, other.Version, ConfigChangeAction.Update, AuditEventType.Update,
                new { other.Code, IsDefault = true }, new { other.Code, IsDefault = false }, $"Default moved to {design.Code}.");
        }

        design.IsDefault = true;
        _designs.Update(design);
        RecordChange(EntityName, design.Id, design.Version, ConfigChangeAction.Update, AuditEventType.Update,
            new { design.Code, IsDefault = false }, new { design.Code, IsDefault = true }, "Made the default specimen label design.");
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<SpecimenLabelDesignDto>.Ok(ToDto(design));
    }

    public async Task<EvaluationResult<SpecimenLabelDesignDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var design = await _designs.GetByIdAsync(id, ct);
        if (design is null)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail("Label design not found.");
        }

        if (!active && design.IsDefault)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Fail("This is the default design. Make another design the default before deactivating it.");
        }

        if (design.IsActive == active)
        {
            return EvaluationResult<SpecimenLabelDesignDto>.Ok(ToDto(design));
        }

        design.IsActive = active;
        _designs.Update(design);
        RecordChange(EntityName, design.Id, design.Version,
            active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate,
            active ? AuditEventType.Activate : AuditEventType.Deactivate,
            new { design.Code, IsActive = !active }, new { design.Code, IsActive = active },
            active ? "Specimen label design activated." : "Specimen label design deactivated.");
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<SpecimenLabelDesignDto>.Ok(ToDto(design));
    }

    public static LabelLayoutDocument? Read(SpecimenLabelDesign design)
    {
        try
        {
            var document = JsonSerializer.Deserialize<LabelLayoutDocument>(design.DocumentJson, LabelFields.JsonOptions);
            if (document is not null)
            {
                document.WidthDots = design.WidthDots;
                document.HeightDots = design.HeightDots;
            }

            return document;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Write(LabelLayoutDocument document) => JsonSerializer.Serialize(document, LabelFields.JsonOptions);

    private static object Snapshot(SpecimenLabelDesign d) =>
        new { d.Code, d.Name, d.Purpose, d.WidthDots, d.HeightDots, d.IsDefault, d.IsActive, d.Version, d.DocumentJson };

    private static SpecimenLabelDesignDto ToDto(SpecimenLabelDesign design)
    {
        var document = Read(design) ?? new LabelLayoutDocument { WidthDots = design.WidthDots, HeightDots = design.HeightDots };
        var check = SpecimenLabelDesignRules.Check(design.Purpose, design.WidthDots, design.HeightDots, document.Page1);
        return new SpecimenLabelDesignDto(design.Id, design.Code, design.Name, design.Purpose, design.WidthDots, design.HeightDots,
            design.IsDefault, design.IsActive, design.Version, document, check.Errors.Concat(check.Warnings).ToList());
    }

    private async Task<EvaluationResult<SpecimenLabelDesignDto>?> RejectAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(CurrentUser.UserName, PermissionCodes.AdminConfigEdit, ct);
        var result = PrinterAuthorizationRule.EvaluateUpdate(allowed);
        return result.Severity == RuleSeverity.HardStop
            ? EvaluationResult<SpecimenLabelDesignDto>.Blocked(new RuleEvaluation([result]))
            : null;
    }
}
