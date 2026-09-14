using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin listing and licensed-catalog import of ISBT lookup rows.
/// Import persists licensee-supplied codes only (OCD-004). It does not invent tables.
/// </summary>
public sealed class IsbtProductCodeAdminService
{
    private readonly IRepository<IsbtProductCode> _codes;
    private readonly IRepository<IsbtAboRhdCode> _abo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public IsbtProductCodeAdminService(
        IRepository<IsbtProductCode> codes,
        IRepository<IsbtAboRhdCode> abo,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _codes = codes;
        _abo = abo;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<IsbtProductCodeDto>> ListAsync(CancellationToken ct = default)
    {
        var asOf = DateOnly.FromDateTime(_clock.UtcNow);
        var list = await _codes.ListAsync(_ => true, ct);
        return list
            .OrderBy(c => c.ProductDescriptionCode, StringComparer.Ordinal)
            .Select(c => ToDto(c, asOf))
            .ToList();
    }

    public async Task<EvaluationResult<LicensedIsbtCatalogImportResult>> ImportLicensedAsync(
        LicensedIsbtCatalogImportRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_permissions is not null)
        {
            var allowed = await _permissions.HasPermissionAsync(
                _currentUser.UserName, PermissionCodes.AdminConfigEdit, ct);
            var auth = IsbtLicensedCatalogImportRule.EvaluatePermission(allowed);
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<LicensedIsbtCatalogImportResult>.Blocked(new RuleEvaluation([auth]));
            }
        }

        var products = request.ProductCodes ?? [];
        var aboRows = request.AboRhdCodes ?? [];
        var gate = IsbtLicensedCatalogImportRule.Evaluate(
            request.LicenseAcknowledgment,
            request.StandardVersion,
            products.Count,
            aboRows.Count);
        if (gate.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<LicensedIsbtCatalogImportResult>.Blocked(new RuleEvaluation([gate]));
        }

        var version = request.StandardVersion.Trim();
        var replaced = 0;
        var importedProducts = 0;
        var importedAbo = 0;

        foreach (var row in products)
        {
            var pdc = (row.ProductDescriptionCode ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(pdc) || string.IsNullOrWhiteSpace(row.Description))
            {
                return EvaluationResult<LicensedIsbtCatalogImportResult>.Fail(
                    "Each product row must include ProductDescriptionCode and Description from the licensed extract.");
            }

            var existing = await _codes.FirstOrDefaultAsync(
                c => c.ProductDescriptionCode == pdc && c.StandardVersion == version, ct);
            if (existing is null)
            {
                var placeholder = await _codes.FirstOrDefaultAsync(
                    c => c.ProductDescriptionCode == pdc && c.IsPlaceholder, ct);
                if (placeholder is not null)
                {
                    ApplyProduct(placeholder, row, pdc, version);
                    placeholder.IsPlaceholder = false;
                    _codes.Update(placeholder);
                    replaced++;
                }
                else
                {
                    var added = new IsbtProductCode();
                    ApplyProduct(added, row, pdc, version);
                    added.IsPlaceholder = false;
                    await _codes.AddAsync(added, ct);
                }
            }
            else
            {
                if (existing.IsPlaceholder)
                    replaced++;
                ApplyProduct(existing, row, pdc, version);
                existing.IsPlaceholder = false;
                _codes.Update(existing);
            }

            importedProducts++;
        }

        foreach (var row in aboRows)
        {
            var code = (row.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code)
                || !Enum.TryParse<AboGroup>(row.Abo, ignoreCase: true, out var abo)
                || !Enum.TryParse<RhType>(row.RhD, ignoreCase: true, out var rh))
            {
                return EvaluationResult<LicensedIsbtCatalogImportResult>.Fail(
                    "Each ABO/RhD row must include Code plus parseable Abo and RhD values from the licensed extract.");
            }

            var existing = await _abo.FirstOrDefaultAsync(
                c => c.Code == code && c.StandardVersion == version, ct);
            if (existing is null)
            {
                var placeholder = await _abo.FirstOrDefaultAsync(
                    c => c.Code == code && c.IsPlaceholder, ct);
                if (placeholder is not null)
                {
                    ApplyAbo(placeholder, row, code, abo, rh, version);
                    placeholder.IsPlaceholder = false;
                    _abo.Update(placeholder);
                    replaced++;
                }
                else
                {
                    var added = new IsbtAboRhdCode();
                    ApplyAbo(added, row, code, abo, rh, version);
                    added.IsPlaceholder = false;
                    await _abo.AddAsync(added, ct);
                }
            }
            else
            {
                if (existing.IsPlaceholder)
                    replaced++;
                ApplyAbo(existing, row, code, abo, rh, version);
                existing.IsPlaceholder = false;
                _abo.Update(existing);
            }

            importedAbo++;
        }

        var result = new LicensedIsbtCatalogImportResult(
            version, importedProducts, importedAbo, replaced);

        _audit.Record(
            AuditEventType.Import,
            nameof(IsbtProductCode),
            entityId: null,
            oldValue: null,
            newValue: result,
            reason: string.IsNullOrWhiteSpace(request.Reason)
                ? "Licensed ISBT catalog import (OCD-004)."
                : request.Reason.Trim());

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<LicensedIsbtCatalogImportResult>.Ok(result, new RuleEvaluation([gate]));
    }

    private static void ApplyProduct(
        IsbtProductCode entity,
        LicensedIsbtProductCodeRow row,
        string pdc,
        string version)
    {
        entity.ProductDescriptionCode = pdc;
        entity.Description = row.Description.Trim();
        entity.ComponentClass = string.IsNullOrWhiteSpace(row.ComponentClass) ? "Other" : row.ComponentClass.Trim();
        entity.Modifier = string.IsNullOrWhiteSpace(row.Modifier) ? null : row.Modifier.Trim();
        entity.StorageRequirements = string.IsNullOrWhiteSpace(row.StorageRequirements)
            ? null
            : row.StorageRequirements.Trim();
        entity.RequiresExtendedDivision = row.RequiresExtendedDivision;
        entity.StandardVersion = version;
        entity.AttributesJson = "[]";
    }

    private static void ApplyAbo(
        IsbtAboRhdCode entity,
        LicensedIsbtAboRhdRow row,
        string code,
        AboGroup abo,
        RhType rh,
        string version)
    {
        entity.Code = code;
        entity.Abo = abo;
        entity.RhD = rh;
        entity.CollectionType = string.IsNullOrWhiteSpace(row.CollectionType) ? null : row.CollectionType.Trim();
        entity.StandardVersion = version;
    }

    private static IsbtProductCodeDto ToDto(IsbtProductCode c, DateOnly asOf) =>
        new(
            c.Id,
            c.ProductDescriptionCode,
            c.Description,
            c.ComponentClass,
            c.Modifier,
            c.StorageRequirements,
            c.RequiresExtendedDivision,
            c.EffectiveDate,
            c.RetiredDate,
            c.StandardVersion,
            c.IsPlaceholder,
            c.RetiredDate is not null && c.RetiredDate < asOf);
}
