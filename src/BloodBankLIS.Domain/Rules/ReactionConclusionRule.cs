using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-057: closing a reaction investigation records the reaction the provider concluded and the treatment chosen
/// for future transfusions, each from its admin list. "No reaction" and "No treatment" are always available. More than
/// one treatment may be chosen, but No treatment cannot be combined with another.
/// </summary>
public static class ReactionConclusionRule
{
    public const string Code = "RXN-CONCLUSION";

    /// <summary>The fixed code of the No reaction and No treatment entries.</summary>
    public const string NoneCode = "NONE";

    public const string NoReactionText = "No reaction";
    public const string NoTreatmentText = "No treatment";

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Checks a conclusion against the active entries of each list. <paramref name="requireComplete"/> is true when
    /// closing: then both the reaction and at least one treatment are required.
    /// </summary>
    public static RuleResult Evaluate(
        string? reactionCode,
        IReadOnlyCollection<string>? treatmentCodes,
        IReadOnlyCollection<ReactionCatalogEntry> activeEntries,
        bool requireComplete)
    {
        var reaction = Normalize(reactionCode);
        var treatments = (treatmentCodes ?? []).Select(Normalize).Where(c => c.Length > 0).Distinct().ToList();

        if (requireComplete && reaction.Length == 0)
        {
            return RuleResult.HardStop(Code, "Choose the reaction concluded, or No reaction, before closing.");
        }

        if (requireComplete && treatments.Count == 0)
        {
            return RuleResult.HardStop(Code, "Choose the treatment for future transfusions, or No treatment, before closing.");
        }

        if (reaction.Length > 0 && !IsListed(reaction, ReactionCatalogKind.ReactionType, activeEntries))
        {
            return RuleResult.HardStop(Code, $"Reaction {reaction} is not on the active reaction list.");
        }

        var unknown = treatments.FirstOrDefault(t => !IsListed(t, ReactionCatalogKind.Treatment, activeEntries));
        if (unknown is not null)
        {
            return RuleResult.HardStop(Code, $"Treatment {unknown} is not on the active treatment list.");
        }

        return treatments.Contains(NoneCode) && treatments.Count > 1
            ? RuleResult.HardStop(Code, "No treatment cannot be chosen together with a treatment.")
            : RuleResult.Pass(Code);
    }

    /// <summary>The text recorded for a code, as the list shows it now (kept on the investigation as written).</summary>
    public static string TextFor(string code, ReactionCatalogKind kind, IReadOnlyCollection<ReactionCatalogEntry> entries) =>
        entries.FirstOrDefault(e => e.Kind == kind && Normalize(e.Code) == code)?.Text
        ?? (code == NoneCode ? (kind == ReactionCatalogKind.ReactionType ? NoReactionText : NoTreatmentText) : code);

    private static bool IsListed(string code, ReactionCatalogKind kind, IReadOnlyCollection<ReactionCatalogEntry> entries) =>
        code == NoneCode || entries.Any(e => e.Kind == kind && e.IsActive && Normalize(e.Code) == code);

    /// <summary>The none entry cannot be deactivated or have its code changed.</summary>
    public static RuleResult EvaluateCatalogChange(bool isNoneOption, string? newCode, bool? newActive)
    {
        if (!isNoneOption)
        {
            return RuleResult.Pass(Code);
        }

        if (newActive == false)
        {
            return RuleResult.HardStop(Code, "No reaction and No treatment must always be available, so they cannot be deactivated.");
        }

        return newCode is not null && Normalize(newCode) != NoneCode
            ? RuleResult.HardStop(Code, "The code of No reaction or No treatment cannot be changed.")
            : RuleResult.Pass(Code);
    }
}
