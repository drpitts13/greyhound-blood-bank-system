using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-069 (decided 2026-11-10, option C): the application suggests RhIG candidacy from the facts on file and calculates
/// the vials from the fetal bleed quantitation with the facility's settings; the reviewer confirms both by recording the
/// decision and vials. The settings ship unset, so nothing is calculated until the medical director enters them. Weak D
/// and partial D are recorded on the patient and treated as Rh negative (decided 2026-11-10, option B).
/// </summary>
public static class RhigDoseRule
{
    /// <summary>How the calculated vial count is rounded (Facility Policies, RhIG dose rounding).</summary>
    public enum Rounding
    {
        NotSet = 0,
        RoundUp = 1,
        NearestPlusOne = 2,
        UpPlusOne = 3
    }

    public sealed record Settings(int MlFetalBloodPerVial, int MaternalBloodVolumeMl, Rounding Rounding)
    {
        public bool IsSet => MlFetalBloodPerVial > 0 && MaternalBloodVolumeMl > 0 && Rounding != Rounding.NotSet;
    }

    /// <summary>
    /// Vials from the quantitation: fetal whole blood (mL) = % fetal cells × maternal blood volume; vials = that ÷ mL per
    /// vial, rounded as set. Null, with the reason, when there is no quantitation or the settings are not all set.
    /// </summary>
    public static (int? Vials, string Explanation) Calculate(FetalBleedScreen screen, decimal? fetalBleedPercent, Settings settings)
    {
        if (screen != FetalBleedScreen.Positive || fetalBleedPercent is not decimal percent)
        {
            return (null, "No dose calculated: it is calculated from a positive fetal bleed screen's quantitation.");
        }

        if (!settings.IsSet)
        {
            return (null, "No dose calculated: the RhIG dose settings (mL of fetal blood per vial, maternal blood volume, rounding) are not set on Facility Policies.");
        }

        var fetalMl = percent / 100m * settings.MaternalBloodVolumeMl;
        var raw = fetalMl / settings.MlFetalBloodPerVial;
        var vials = settings.Rounding switch
        {
            Rounding.RoundUp => Math.Max(1, (int)Math.Ceiling(raw)),
            Rounding.NearestPlusOne => (int)Math.Round(raw, MidpointRounding.AwayFromZero) + 1,
            _ => (int)Math.Ceiling(raw) + 1
        };
        var how = settings.Rounding switch
        {
            Rounding.RoundUp => "rounded up",
            Rounding.NearestPlusOne => "rounded to the nearest and one added",
            _ => "rounded up and one added"
        };
        return (vials,
            $"{percent:0.###}% × {settings.MaternalBloodVolumeMl} mL = {fetalMl:0.#} mL fetal blood; ÷ {settings.MlFetalBloodPerVial} mL per vial = {raw:0.##}; {how}: {vials} vial{(vials == 1 ? "" : "s")}.");
    }

    /// <summary>
    /// The suggested decision from the facts on file, with the reason; null when the facts do not settle it (no Rh type,
    /// or anti-D on history, which may be passive from earlier RhIG or immune: the reviewer decides).
    /// </summary>
    public static (RhigDecision? Suggested, string Reason) Suggest(RhType? patientRh, bool antiDOnHistory, RhType infantRh,
        RhDVariant variant = RhDVariant.None)
    {
        // OCD-069 (decided 2026-11-10, option B): any weak D or partial D is treated as Rh negative for RhIG.
        var variantNote = variant switch
        {
            RhDVariant.WeakD => " (weak D, treated as Rh negative for RhIG)",
            RhDVariant.PartialD => " (partial D, treated as Rh negative for RhIG)",
            _ => string.Empty
        };
        if (variant != RhDVariant.None)
        {
            patientRh = RhType.Negative;
        }

        var (suggested, reason) = SuggestFor(patientRh, antiDOnHistory, infantRh);
        return (suggested, variantNote.Length == 0 ? reason : reason.Replace("the patient is Rh negative", "the patient is Rh negative" + variantNote));
    }

    private static (RhigDecision? Suggested, string Reason) SuggestFor(RhType? patientRh, bool antiDOnHistory, RhType infantRh)
    {
        if (patientRh is null or RhType.Unknown)
        {
            return (null, "No suggestion: no current Rh type on file for the patient.");
        }

        if (patientRh == RhType.Positive)
        {
            return (RhigDecision.NotCandidate, "Suggested not a candidate: the patient is Rh positive on file.");
        }

        if (antiDOnHistory)
        {
            return (null, "No suggestion: anti-D is on the antibody history. Whether it is passive (from earlier RhIG) or immune is the reviewer's decision.");
        }

        return infantRh == RhType.Negative
            ? (RhigDecision.NotCandidate, "Suggested not a candidate: the infant is Rh negative.")
            : (RhigDecision.Candidate, infantRh == RhType.Positive
                ? "Suggested candidate: the patient is Rh negative with no anti-D, and the infant is Rh positive."
                : "Suggested candidate: the patient is Rh negative with no anti-D, and the infant's Rh is not known.");
    }
}
