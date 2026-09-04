using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Isbt128;

/// <summary>
/// Builds a canonical component from structured human-readable fields and receives it.
/// Derives scanner representation; does not allow editing derived fields outside correction workflow.
/// </summary>
public sealed class ManualComponentEntryService
{
    private readonly IsbtLookupCatalog _lookups;
    private readonly IDinCheckCharacterValidator _dinCheck;
    private readonly InventoryService _inventory;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;
    private readonly IPermissionEvaluator? _permissions;

    public ManualComponentEntryService(
        IsbtLookupCatalog lookups,
        IDinCheckCharacterValidator dinCheck,
        InventoryService inventory,
        IClock clock,
        ICurrentUser user,
        IPermissionEvaluator? permissions = null)
    {
        _lookups = lookups;
        _dinCheck = dinCheck;
        _inventory = inventory;
        _clock = clock;
        _user = user;
        _permissions = permissions;
    }

    public async Task<InventoryActionResult> CreateAsync(ManualComponentEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(request.DonationNumber))
            return InventoryActionResult.Fail($"{IsbtErrorCodes.InvalidDinLength}: Donation/unit number is required.");

        // Combined human-readable DIN (spaces optional; keyboard check optional unless required).
        var requireCheck = !request.AllowDinCheckException
            && request.DonationNumber.Trim().Replace(" ", "").Length >= 14;
        var din = DinParser.Parse(
            request.DonationNumber,
            _dinCheck,
            requireKeyboardCheck: requireCheck);

        if (!din.Success)
        {
            if (request.AllowDinCheckException
                && din.Errors.Any(e => e.Code == IsbtErrorCodes.DinCheckMismatch)
                && !string.IsNullOrWhiteSpace(request.DinCheckExceptionReason))
            {
                // Controlled exception: parse without requiring check match.
                din = DinParser.Parse(
                    request.DonationNumber,
                    checkValidator: null,
                    requireKeyboardCheck: false);
            }

            if (!din.Success)
                return InventoryActionResult.Fail(string.Join("; ", din.Errors.Select(e => $"{e.Code}: {e.Message}")));
        }

        var aboLookup = await _lookups.GetAboLookupAsync(ct);
        var abo = AboRhdParser.FromStructured(request.AboRhdCode, aboLookup);
        if (!abo.Success)
            return InventoryActionResult.Fail(string.Join("; ", abo.Errors.Select(e => $"{e.Code}: {e.Message}")));

        var productLookup = await _lookups.GetProductLookupAsync(ct);
        var product = ProductParser.FromStructured(
            request.ProductDescriptionCode,
            request.CollectionTypeCode,
            request.DivisionCode,
            productLookup,
            request.ExtendedDivisionCode);
        if (!product.Success)
            return InventoryActionResult.Fail(string.Join("; ", product.Errors.Select(e => $"{e.Code}: {e.Message}")));

        var expiration = ExpirationParser.FromLocalDateTime(request.ExpirationLocal, request.ExpirationHasExplicitTime);
        if (!expiration.Success)
            return InventoryActionResult.Fail(string.Join("; ", expiration.Errors.Select(e => $"{e.Code}: {e.Message}")));

        var draft = new CanonicalComponentDraft
        {
            Din = din.Value,
            AboRhd = abo.Value,
            Product = product.Value,
            Expiration = expiration.Value,
            Source = ComponentEntrySource.Manual,
            EnteredBy = _user.UserName,
            EnteredAt = _clock.UtcNow
        };
        draft.RebuildIdentity();

        return await _inventory.ReceiveNormalizedComponentAsync(
            draft,
            request.ProductTypeId,
            request.LocationId,
            request.Supplier,
            request.ShipmentId,
            request.CollectionFacility,
            request.Volume,
            request.ReleaseToAvailable,
            request.VisualInspectionAcceptable,
            request.VisualInspectionNotes,
            request.Appearance,
            request.SecondVerifier,
            request.ReceiveTemperatureCelsius,
            request.DonationRestriction,
            request.ReservedPatientId,
            ct);
    }

    private async Task<InventoryActionResult?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _user.UserName, PermissionCodes.InventoryReceive, ct);
        var auth = InventoryAuthorizationRule.EvaluateReceive(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
