using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Domain.Isbt128.Validation;

/// <summary>Field-level validation for normalized ISBT quadrant values.</summary>
public static class ComponentFieldValidator
{
    public static ValidationResult Validate(CanonicalComponentDraft draft, IDinCheckCharacterValidator? dinCheck = null)
    {
        var errors = new List<ValidationMessage>();
        var warnings = new List<ValidationMessage>();

        if (draft.Din is null)
            errors.Add(ValidationMessage.Error(IsbtErrorCodes.IncompleteScanSession, "DIN is required.", "din"));
        else
        {
            if (draft.Din.Din.Length != 13)
                errors.Add(ValidationMessage.Error(IsbtErrorCodes.InvalidDinLength, "DIN must be 13 characters.", "din"));
            if (draft.Din.Flags.Length != 2)
                errors.Add(ValidationMessage.Error(IsbtErrorCodes.InvalidFlagLength, "Flags must be 2 characters.", "dinFlags"));
            if (!string.IsNullOrEmpty(draft.Din.KeyboardCheck) && dinCheck is not null
                && !dinCheck.IsValid(draft.Din.Din, draft.Din.KeyboardCheck[0]))
            {
                errors.Add(ValidationMessage.Error(
                    IsbtErrorCodes.DinCheckMismatch,
                    "Keyboard check character mismatch.",
                    "dinKeyboardCheck",
                    overrideAllowed: true,
                    requiredRole: "Supervisor"));
            }
        }

        if (draft.AboRhd is null)
            errors.Add(ValidationMessage.Error(IsbtErrorCodes.IncompleteScanSession, "ABO/RhD is required.", "aboRhd"));
        else if (string.IsNullOrWhiteSpace(draft.AboRhd.AboRhdCode))
            errors.Add(ValidationMessage.Error(IsbtErrorCodes.UnknownAboRhdCode, "ABO/RhD code missing.", "aboRhd"));

        if (draft.Product is null)
            errors.Add(ValidationMessage.Error(IsbtErrorCodes.IncompleteScanSession, "Product is required.", "product"));
        else
        {
            if (draft.Product.ProductCodeData.Length != 8)
                errors.Add(ValidationMessage.Error(IsbtErrorCodes.UnknownProductCode, "Product data must be 8 characters.", "product"));
            if (draft.Product.RequiresExtendedDivision && string.IsNullOrWhiteSpace(draft.Product.ExtendedDivisionCode))
                errors.Add(ValidationMessage.Error(IsbtErrorCodes.ExtendedDivisionRequired, "Extended division required.", "extendedDivision"));
            if (draft.Product.IsRetired)
                warnings.Add(ValidationMessage.Warning(IsbtErrorCodes.RetiredProductNotAllowed, "Product code is retired; confirm existing-inventory policy.", "product"));
        }

        if (draft.Expiration is null)
            errors.Add(ValidationMessage.Error(IsbtErrorCodes.IncompleteScanSession, "Expiration is required.", "expiration"));

        return errors.Count == 0
            ? ValidationResult.Success(warnings.ToArray())
            : ValidationResult.Failure(errors, warnings);
    }
}
