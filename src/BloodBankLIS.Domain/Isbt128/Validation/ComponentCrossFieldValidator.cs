namespace BloodBankLIS.Domain.Isbt128.Validation;

/// <summary>Cross-field validation for a canonical component draft prior to persistence.</summary>
public static class ComponentCrossFieldValidator
{
    public static ValidationResult Validate(
        CanonicalComponentDraft draft,
        bool identityAlreadyExists,
        DateTime utcNow,
        TimeZoneInfo? facilityTimeZone = null)
    {
        var errors = new List<ValidationMessage>();
        var warnings = new List<ValidationMessage>();

        var field = ComponentFieldValidator.Validate(draft);
        errors.AddRange(field.Errors);
        warnings.AddRange(field.Warnings);

        if (!draft.HasRequiredQuadrants)
        {
            errors.Add(ValidationMessage.Error(
                IsbtErrorCodes.IncompleteScanSession,
                "Standard unit receipt requires DIN, ABO/RhD, Product, and Expiration."));
        }

        draft.RebuildIdentity();
        if (string.IsNullOrEmpty(draft.ComponentIdentity))
        {
            errors.Add(ValidationMessage.Error(
                IsbtErrorCodes.IncompleteScanSession,
                "Component identity could not be built."));
        }
        else if (identityAlreadyExists)
        {
            errors.Add(ValidationMessage.Error(
                IsbtErrorCodes.ComponentDuplicate,
                $"Component identity '{draft.ComponentIdentity}' already exists."));
        }

        if (draft.Expiration is not null)
        {
            var tz = facilityTimeZone ?? TimeZoneInfo.Utc;
            DateTime expirationUtc;
            try
            {
                expirationUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(draft.Expiration.ExpirationLocal, DateTimeKind.Unspecified),
                    tz);
            }
            catch
            {
                expirationUtc = DateTime.SpecifyKind(draft.Expiration.ExpirationLocal, DateTimeKind.Utc);
            }

            if (expirationUtc <= utcNow)
            {
                errors.Add(ValidationMessage.Error(
                    IsbtErrorCodes.ComponentExpired,
                    "Expiration is in the past for normal receipt.",
                    "expiration",
                    overrideAllowed: true,
                    requiredRole: "Supervisor"));
            }
        }

        if (draft.Product?.RequiresExtendedDivision == true
            && string.IsNullOrWhiteSpace(draft.Product.ExtendedDivisionCode))
        {
            errors.Add(ValidationMessage.Error(
                IsbtErrorCodes.ExtendedDivisionRequired,
                "Extended division must be present before the component can be finalized.",
                "extendedDivision"));
        }

        // Deduplicate by code+field
        errors = errors
            .GroupBy(e => (e.Code, e.Field, e.Message))
            .Select(g => g.First())
            .ToList();

        return errors.Count == 0
            ? ValidationResult.Success(warnings.ToArray())
            : ValidationResult.Failure(errors, warnings);
    }
}
