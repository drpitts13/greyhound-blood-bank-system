using System.Globalization;
using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

public enum IndicationComparison
{
    LessThan = 0,
    LessOrEqual = 1,
    GreaterThan = 2,
    GreaterOrEqual = 3
}

public enum PbmStatus
{
    /// <summary>Evaluation is off, or the line is not a product.</summary>
    NotEvaluated = 0,

    Met = 1,

    OutsideCriteria = 2,

    /// <summary>Evaluation is on but the facility has no active criteria for this component.</summary>
    NoCriteria = 3
}

/// <summary>A numeric laboratory result available at order time.</summary>
public sealed record IndicationLabValue(string TestCode, decimal Value, string? Unit, DateTime ResultUtc);

public sealed record IndicationOutcome(PbmStatus Status, string Detail, string? MetCriterionCode);

/// <summary>
/// Patient-blood-management ordering check against facility criteria. It never blocks a
/// transfusion by itself: an order outside criteria is recorded for review, and facility
/// policy may require the orderer to give a reason. The software supplies no thresholds;
/// the facility enters them after SME approval (OCD-038).
/// </summary>
public static class TransfusionIndicationRule
{
    public const string JustificationCode = "PBM-JUSTIFY";

    /// <summary>A criterion still marked DRAFT cannot be active (OCD-038): the committee reviews and renames it first.</summary>
    public const string DraftCode = "PBM-DRAFT";
    public const int MaxLookbackHours = 720;

    public static bool Compare(decimal value, IndicationComparison comparison, decimal threshold) => comparison switch
    {
        IndicationComparison.LessThan => value < threshold,
        IndicationComparison.LessOrEqual => value <= threshold,
        IndicationComparison.GreaterThan => value > threshold,
        IndicationComparison.GreaterOrEqual => value >= threshold,
        _ => false
    };

    public static string Symbol(IndicationComparison comparison) => comparison switch
    {
        IndicationComparison.LessThan => "<",
        IndicationComparison.LessOrEqual => "≤",
        IndicationComparison.GreaterThan => ">",
        _ => "≥"
    };

    public static bool IsLaboratory(TransfusionIndicationCriterion c) => !string.IsNullOrWhiteSpace(c.LabTestCode);

    /// <summary>
    /// Met when any active laboratory criterion is satisfied by the latest result inside its
    /// look-back, or when the orderer selected an active attestation criterion.
    /// </summary>
    public static IndicationOutcome Evaluate(
        IReadOnlyCollection<TransfusionIndicationCriterion> criteria,
        IReadOnlyCollection<IndicationLabValue> labs,
        DateTime orderedUtc,
        string? selectedAttestationCode)
    {
        var active = criteria.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Code).ToList();
        if (active.Count == 0)
        {
            return new IndicationOutcome(PbmStatus.NoCriteria, "No facility criteria are set up for this component.", null);
        }

        var notes = new List<string>();
        foreach (var criterion in active.Where(IsLaboratory))
        {
            var since = orderedUtc.AddHours(-Math.Clamp(criterion.LookbackHours, 1, MaxLookbackHours));
            var latest = labs
                .Where(l => string.Equals(l.TestCode, criterion.LabTestCode, StringComparison.OrdinalIgnoreCase)
                            && l.ResultUtc <= orderedUtc && l.ResultUtc >= since)
                .OrderByDescending(l => l.ResultUtc)
                .FirstOrDefault();
            var rule = $"{criterion.LabTestCode} {Symbol(criterion.Comparison!.Value)} {Format(criterion.Threshold!.Value)}{UnitText(criterion.Unit)}";
            if (latest is null)
            {
                notes.Add($"{rule}: no result in the last {criterion.LookbackHours} h");
                continue;
            }

            var observed = $"{latest.TestCode} {Format(latest.Value)}{UnitText(latest.Unit ?? criterion.Unit)} at {latest.ResultUtc:yyyy-MM-dd HH:mm} UTC";
            if (Compare(latest.Value, criterion.Comparison!.Value, criterion.Threshold!.Value))
            {
                return new IndicationOutcome(PbmStatus.Met, $"{criterion.Code}: {observed} meets {rule}.", criterion.Code);
            }

            notes.Add($"{rule}: latest {observed}");
        }

        if (!string.IsNullOrWhiteSpace(selectedAttestationCode))
        {
            var chosen = active.FirstOrDefault(c => !IsLaboratory(c)
                && string.Equals(c.Code, selectedAttestationCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (chosen is not null)
            {
                return new IndicationOutcome(PbmStatus.Met, $"{chosen.Code}: {chosen.Name} (selected by the orderer).", chosen.Code);
            }
        }

        var detail = notes.Count == 0
            ? "No indication selected."
            : string.Join("; ", notes) + ".";
        return new IndicationOutcome(PbmStatus.OutsideCriteria, detail, null);
    }

    /// <summary>A reason is required when policy asks for one and the order is outside criteria without a justification.</summary>
    public static RuleResult EvaluateJustification(PbmStatus status, bool policyRequiresReason, string? justification) =>
        status == PbmStatus.OutsideCriteria && policyRequiresReason && string.IsNullOrWhiteSpace(justification)
            ? RuleResult.HardStop(JustificationCode, "This order is outside the facility's transfusion criteria. Select an indication or enter a reason.")
            : RuleResult.Pass(JustificationCode);

    public static RuleEvaluation Validate(TransfusionIndicationCriterion c, bool duplicateCode)
    {
        var results = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(c.Code) || c.Code.Length > 20)
        {
            results.Add(RuleResult.HardStop("PBM.CODE", "A code of 1 to 20 characters is required."));
        }

        if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 200)
        {
            results.Add(RuleResult.HardStop("PBM.NAME", "A name of 1 to 200 characters is required."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop("PBM.CODE.DUPLICATE", $"Code '{c.Code}' is already in use."));
        }

        if (IsLaboratory(c))
        {
            if (c.Comparison is null || c.Threshold is null)
            {
                results.Add(RuleResult.HardStop("PBM.LAB", "A laboratory criterion needs a comparison and a threshold."));
            }

            if (c.LookbackHours is < 1 or > MaxLookbackHours)
            {
                results.Add(RuleResult.HardStop("PBM.LOOKBACK", $"Look-back must be 1 to {MaxLookbackHours} hours."));
            }
        }
        else if (c.Comparison is not null || c.Threshold is not null)
        {
            results.Add(RuleResult.HardStop("PBM.ATTEST", "A comparison and threshold need a test code."));
        }

        if (c.IsActive && PbmDraftCriteria.IsDraft(c.Code, c.Name))
        {
            results.Add(RuleResult.HardStop(DraftCode,
                "This criterion is still an unverified draft. Have the transfusion committee review it, map the test code, and rename it (remove DRAFT from the code and name) before activating."));
        }

        return new RuleEvaluation(results);
    }

    private static string Format(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string UnitText(string? unit) => string.IsNullOrWhiteSpace(unit) ? string.Empty : " " + unit.Trim();
}
