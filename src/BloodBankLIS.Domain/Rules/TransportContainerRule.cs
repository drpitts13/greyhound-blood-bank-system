using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Transport container setup and MTP packing checks. The plausibility limits catch typing errors only; they are not
/// storage specifications. Any acceptable range comes from the facility's own container validation.
/// </summary>
public static class TransportContainerRule
{
    public const string ContainerCode = "XPORT-CONTAINER";
    public const string PackCode = "MTP-PACKING";
    public const decimal LowestPlausibleC = -40m;
    public const decimal HighestPlausibleC = 60m;
    public const int MaxValidatedHours = 168;

    public static RuleEvaluation Validate(TransportContainer c, bool duplicateCode)
    {
        var results = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(c.Code))
        {
            results.Add(RuleResult.HardStop(ContainerCode, "Enter a code for the container."));
        }
        else if (c.Code.Trim().Length > 20)
        {
            results.Add(RuleResult.HardStop(ContainerCode, "The container code can be at most 20 characters."));
        }
        else if (duplicateCode)
        {
            results.Add(RuleResult.HardStop(ContainerCode, $"Another container already uses the code {c.Code.Trim()}."));
        }

        if (string.IsNullOrWhiteSpace(c.Name))
        {
            results.Add(RuleResult.HardStop(ContainerCode, "Enter a name for the container."));
        }

        if (!Plausible(c.MinTemperatureC) || !Plausible(c.MaxTemperatureC))
        {
            results.Add(RuleResult.HardStop(ContainerCode, $"Temperatures must be between {LowestPlausibleC} and {HighestPlausibleC} °C."));
        }
        else if (c.MinTemperatureC is decimal min && c.MaxTemperatureC is decimal max && min >= max)
        {
            results.Add(RuleResult.HardStop(ContainerCode, "The lowest temperature must be below the highest."));
        }

        if (c.ValidatedHours is int hours && (hours < 1 || hours > MaxValidatedHours))
        {
            results.Add(RuleResult.HardStop(ContainerCode, $"Validated hours must be from 1 to {MaxValidatedHours}, or left blank."));
        }

        if (results.Count == 0)
        {
            results.Add(RuleResult.Pass(ContainerCode));
        }

        return new RuleEvaluation(results);
    }

    /// <summary>False when the container has no facility range on the side being checked.</summary>
    public static bool IsOutsideRange(TransportContainer c, decimal temperatureC) =>
        (c.MinTemperatureC is decimal min && temperatureC < min)
        || (c.MaxTemperatureC is decimal max && temperatureC > max);

    /// <summary>
    /// Packing needs an active protocol, an active container, a plausible temperature reading, and at least one
    /// eligible unit. An eligible unit was issued under this protocol, is still issued, and is in no other pack.
    /// A reading outside the container's facility range is a warning: it is recorded and printed, not blocked.
    /// </summary>
    public static RuleEvaluation EvaluatePacking(
        MtpActivationStatus activationStatus,
        TransportContainer? container,
        decimal? temperatureC,
        int selectedUnits,
        IReadOnlyList<string> ineligibleUnits)
    {
        var results = new List<RuleResult>();
        if (activationStatus != MtpActivationStatus.Active)
        {
            results.Add(RuleResult.HardStop(PackCode, "This massive transfusion protocol is closed. Packs can only be recorded while it is active."));
        }

        if (container is null || !container.IsActive)
        {
            results.Add(RuleResult.HardStop(PackCode, "Choose an active transport container."));
        }

        if (temperatureC is not decimal t)
        {
            results.Add(RuleResult.HardStop(PackCode, "Record the temperature of the pack."));
        }
        else if (!Plausible(t))
        {
            results.Add(RuleResult.HardStop(PackCode, $"A reading of {t} °C is not plausible. Check the thermometer and re-enter it."));
        }
        else if (container is not null && IsOutsideRange(container, t))
        {
            results.Add(RuleResult.Warning(PackCode,
                $"{t} °C is outside the range your facility set for {container.Name}. It will be recorded and printed on the pack report."));
        }

        if (selectedUnits == 0)
        {
            results.Add(RuleResult.HardStop(PackCode, "Choose at least one issued unit for the pack."));
        }

        if (ineligibleUnits.Count > 0)
        {
            results.Add(RuleResult.HardStop(PackCode,
                $"These units cannot go in this pack (not issued under this protocol, no longer issued, or already packed): {string.Join(", ", ineligibleUnits)}."));
        }

        if (results.Count == 0)
        {
            results.Add(RuleResult.Pass(PackCode));
        }

        return new RuleEvaluation(results);
    }

    private static bool Plausible(decimal? value) =>
        value is null || (value >= LowestPlausibleC && value <= HighestPlausibleC);
}
