using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Isbt128;

/// <summary>
/// Controlled identity-correction transaction. Blocks silent overwrite after clinical workflow.
/// MEDICAL_DIRECTOR_APPROVAL / INSTITUTIONAL_POLICY_REVIEW for post-transfusion corrections.
/// </summary>
public sealed class ComponentIdentityCorrectionService
{
    private static readonly HashSet<string> CorrectableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Din", "ProductCodeData", "DivisionCode", "ExtendedDivisionCode", "AboRhdCode", "ExpirationEncoded"
    };

    private static readonly UnitStatus[] LockedStatuses =
    [
        UnitStatus.Issued, UnitStatus.TransfusionStarted, UnitStatus.Transfused, UnitStatus.TransfusionStopped
    ];

    private readonly IInventoryRepository _inventory;
    private readonly IRepository<BloodComponentIdentityCorrection> _corrections;
    private readonly IsbtLookupCatalog _lookups;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public ComponentIdentityCorrectionService(
        IInventoryRepository inventory,
        IRepository<BloodComponentIdentityCorrection> corrections,
        IsbtLookupCatalog lookups,
        IUnitOfWork uow,
        IClock clock,
        ICurrentUser user,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _inventory = inventory;
        _corrections = corrections;
        _lookups = lookups;
        _uow = uow;
        _clock = clock;
        _user = user;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<OperationResult<BloodComponentIdentityCorrection>> CorrectAsync(
        CorrectIdentityRequest request,
        bool postEventAuthorized = false,
        CancellationToken ct = default)
    {
        if (_permissions is not null)
        {
            var allowed = await _permissions.HasPermissionAsync(
                _user.UserName, PermissionCodes.InventoryCorrectIdentity, ct);
            var auth = InventoryAuthorizationRule.EvaluateCorrectIdentity(allowed);
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<BloodComponentIdentityCorrection>.Fail(auth.Message);
            }
        }

        if (!CorrectableFields.Contains(request.Field))
            return OperationResult<BloodComponentIdentityCorrection>.Fail(
                $"{IsbtErrorCodes.ComponentIdentityLocked}: Field '{request.Field}' is not correctable.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return OperationResult<BloodComponentIdentityCorrection>.Fail("Correction reason is required.");

        var unit = await _inventory.GetUnitAsync(request.BloodProductId, ct);
        if (unit is null)
            return OperationResult<BloodComponentIdentityCorrection>.Fail("Unit not found.");

        if (LockedStatuses.Contains(unit.Status) && !postEventAuthorized)
        {
            return OperationResult<BloodComponentIdentityCorrection>.Fail(
                $"{IsbtErrorCodes.ComponentIdentityLocked}: Identity correction after issue/transfusion requires authorized post-event workflow.");
        }

        if (unit.Status == UnitStatus.Transfused && !postEventAuthorized)
        {
            return OperationResult<BloodComponentIdentityCorrection>.Fail(
                $"{IsbtErrorCodes.ComponentAlreadyTransfused}: Post-transfusion identity correction blocked.");
        }

        ProductCodeLookupValidator.ResolvedProductCode? resolvedProduct = null;
        if (string.Equals(request.Field, "ProductCodeData", StringComparison.OrdinalIgnoreCase))
        {
            var productLookup = await _lookups.GetProductLookupAsync(ct);
            var productValidation = ProductCodeLookupValidator.Validate(
                request.CorrectedValue,
                productLookup,
                DateOnly.FromDateTime(_clock.UtcNow));
            if (!productValidation.Success)
                return OperationResult<BloodComponentIdentityCorrection>.Fail(productValidation.Error!);

            resolvedProduct = productValidation.Value!;
            if (string.IsNullOrEmpty(resolvedProduct.ProductCodeData))
            {
                return OperationResult<BloodComponentIdentityCorrection>.Fail(
                    $"{IsbtErrorCodes.UnknownProductCode}: Product code correction requires 8-character product code data.");
            }
        }

        var original = GetField(unit, request.Field);
        if (resolvedProduct is not null)
            ApplyResolvedProductCode(unit, resolvedProduct);
        else
            SetField(unit, request.Field, request.CorrectedValue);
        RebuildIdentity(unit);

        if (!string.IsNullOrEmpty(unit.ComponentIdentityKey)
            && await _inventory.ComponentIdentityKeyExistsAsync(unit.ComponentIdentityKey, ct))
        {
            // Revert optimistic uniqueness check — another unit may own the key.
            // Note: current unit also matches; exclude self via identity change detection.
            var existing = await _inventory.GetByComponentIdentityAsync(unit.ComponentIdentity!, ct);
            if (existing is not null && existing.Id != unit.Id)
            {
                SetField(unit, request.Field, original);
                RebuildIdentity(unit);
                return OperationResult<BloodComponentIdentityCorrection>.Fail(
                    $"{IsbtErrorCodes.ComponentDuplicate}: Corrected identity collides with an existing component.");
            }
        }

        var correction = new BloodComponentIdentityCorrection
        {
            BloodProductId = unit.Id,
            Field = request.Field,
            OriginalValue = original,
            CorrectedValue = request.CorrectedValue,
            Reason = request.Reason,
            CorrectedBy = _user.UserName,
            ApproverId = request.ApproverId,
            CorrectedAt = _clock.UtcNow,
            SupportingEvidence = request.SupportingEvidence,
            RevalidationRequired = true,
            RevalidationCompleted = false
        };

        await _corrections.AddAsync(correction, ct);
        _audit.Record(
            AuditEventType.Correct,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Field = request.Field, Value = original },
            newValue: new { Field = request.Field, Value = request.CorrectedValue },
            reason: request.Reason);

        await _uow.SaveChangesAsync(ct);
        return OperationResult<BloodComponentIdentityCorrection>.Ok(correction);
    }

    private static string GetField(BloodUnit unit, string field) => field.ToLowerInvariant() switch
    {
        "din" => unit.Din ?? string.Empty,
        "productcodedata" => unit.ProductCodeData ?? string.Empty,
        "divisioncode" => unit.DivisionCode ?? string.Empty,
        "extendeddivisioncode" => unit.ExtendedDivisionCode ?? string.Empty,
        "aborhdcode" => unit.AboRhdCode ?? string.Empty,
        "expirationencoded" => unit.ExpirationEncoded ?? string.Empty,
        _ => string.Empty
    };

    private static void SetField(BloodUnit unit, string field, string value)
    {
        switch (field.ToLowerInvariant())
        {
            case "din":
                unit.Din = value;
                unit.Isbt128DonationId = value;
                if (value.Length >= 13)
                {
                    unit.Fin = value[..5];
                    unit.NominalYear = value[5..7];
                    unit.DonationSequence = value[7..13];
                }
                break;
            case "productcodedata":
                unit.ProductCodeData = value;
                unit.Isbt128ProductCode = value;
                if (value.Length == 8)
                {
                    unit.ProductDescriptionCode = value[..5];
                    unit.CollectionTypeCode = value[5..6];
                    unit.DivisionCode = value[6..8];
                }
                break;
            case "divisioncode":
                unit.DivisionCode = value;
                if (!string.IsNullOrEmpty(unit.ProductDescriptionCode) && !string.IsNullOrEmpty(unit.CollectionTypeCode))
                    unit.ProductCodeData = unit.ProductDescriptionCode + unit.CollectionTypeCode + value;
                break;
            case "extendeddivisioncode":
                unit.ExtendedDivisionCode = string.IsNullOrWhiteSpace(value) ? null : value;
                break;
            case "aborhdcode":
                unit.AboRhdCode = value;
                break;
            case "expirationencoded":
                unit.ExpirationEncoded = value;
                break;
        }
    }

    private static void ApplyResolvedProductCode(
        BloodUnit unit,
        ProductCodeLookupValidator.ResolvedProductCode resolved)
    {
        unit.ProductCodeData = resolved.ProductCodeData;
        unit.Isbt128ProductCode = resolved.ProductCodeData ?? resolved.ProductDescriptionCode;
        unit.ProductDescriptionCode = resolved.ProductDescriptionCode;
        unit.CollectionTypeCode = resolved.CollectionTypeCode;
        unit.DivisionCode = resolved.DivisionCode;
    }

    private static void RebuildIdentity(BloodUnit unit)
    {
        if (string.IsNullOrEmpty(unit.Din) || string.IsNullOrEmpty(unit.ProductCodeData))
            return;

        unit.ComponentIdentity = ComponentIdentityBuilder.Build(unit.Din, unit.ProductCodeData, unit.ExtendedDivisionCode);
        unit.ComponentIdentityKey = ComponentIdentityBuilder.BuildUniquenessKey(unit.Din, unit.ProductCodeData, unit.ExtendedDivisionCode);
        unit.UnitNumber = unit.ComponentIdentity;
    }
}
