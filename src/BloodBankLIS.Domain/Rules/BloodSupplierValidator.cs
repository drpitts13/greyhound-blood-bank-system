namespace BloodBankLIS.Domain.Rules;

public static class BloodSupplierValidator
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 150;
    public const int AddressMaxLength = 500;
    public const int RegistrationMaxLength = 80;

    public static RuleEvaluation Validate(Entities.BloodSupplier supplier, bool duplicateCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(supplier.Code))
        {
            results.Add(RuleResult.HardStop("SUPPLIER.CODE.REQUIRED", "Supplier code is required."));
        }
        else if (supplier.Code.Trim().Length > CodeMaxLength)
        {
            results.Add(RuleResult.HardStop("SUPPLIER.CODE.LENGTH", $"Supplier code must be {CodeMaxLength} characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            results.Add(RuleResult.HardStop("SUPPLIER.NAME.REQUIRED", "Supplier name is required."));
        }
        else if (supplier.Name.Trim().Length > NameMaxLength)
        {
            results.Add(RuleResult.HardStop("SUPPLIER.NAME.LENGTH", $"Supplier name must be {NameMaxLength} characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(supplier.Registration))
        {
            results.Add(RuleResult.HardStop("SUPPLIER.REGISTRATION.REQUIRED", "Supplier registration is required."));
        }
        else if (supplier.Registration.Trim().Length > RegistrationMaxLength)
        {
            results.Add(RuleResult.HardStop("SUPPLIER.REGISTRATION.LENGTH", $"Supplier registration must be {RegistrationMaxLength} characters or fewer."));
        }

        if (!string.IsNullOrWhiteSpace(supplier.Address) && supplier.Address.Trim().Length > AddressMaxLength)
        {
            results.Add(RuleResult.HardStop("SUPPLIER.ADDRESS.LENGTH", $"Supplier address must be {AddressMaxLength} characters or fewer."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop("SUPPLIER.CODE.DUPLICATE", $"Another supplier already uses code '{supplier.Code}'."));
        }

        return new RuleEvaluation(results);
    }
}
