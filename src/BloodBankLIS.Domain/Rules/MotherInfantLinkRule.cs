using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Linking an infant's chart to the mother's chart, so each shows the other's ABO/Rh and antibodies (maternal antibodies
/// can be present in the infant; the mother's RhIG work needs the infant's type). Information only: the link changes no
/// product selection or testing rule (neonatal policies are OCD-074). The mother must be another active chart, born
/// before the infant, and not the infant's own child.
/// </summary>
public static class MotherInfantLinkRule
{
    public const string Code = "PAT-MOTHER";

    /// <summary>Link mother is offered on charts of patients under one year old (it stays possible through the API).</summary>
    public static bool OfferLink(DateOnly birthDate, DateOnly today) => birthDate > today.AddYears(-1);

    /// <summary>
    /// For a delivery (postpartum) RhIG evaluation: the Rh of the one linked infant born within two days of the event,
    /// when its type is on file; null otherwise (no infant, several, or none typed). A suggestion the user confirms.
    /// </summary>
    public static RhType? SuggestInfantRh(RhigContext context, DateTime eventUtc, IEnumerable<(DateOnly BirthDate, RhType? Rh)> infants)
    {
        if (context != RhigContext.Postpartum)
        {
            return null;
        }

        var eventDay = DateOnly.FromDateTime(eventUtc);
        var near = infants.Where(i => Math.Abs(i.BirthDate.DayNumber - eventDay.DayNumber) <= 2).ToList();
        return near.Count == 1 && near[0].Rh is RhType rh && rh != RhType.Unknown ? rh : null;
    }

    public static RuleResult Evaluate(
        long infantId,
        PatientStatus infantStatus,
        DateOnly infantBirthDate,
        long? motherId,
        PatientStatus? motherStatus,
        DateOnly? motherBirthDate,
        long? mothersOwnMotherId)
    {
        if (infantStatus == PatientStatus.Merged)
        {
            return RuleResult.HardStop(Code, "This chart was merged into another; link the mother on the surviving chart.");
        }

        if (motherId is null)
        {
            return RuleResult.HardStop(Code, "No patient has that MRN; register the mother first, or check the MRN.");
        }

        if (motherId == infantId)
        {
            return RuleResult.HardStop(Code, "A chart cannot be linked as its own mother.");
        }

        if (motherStatus == PatientStatus.Merged)
        {
            return RuleResult.HardStop(Code, "That chart was merged into another; enter the MRN of the chart it was merged into.");
        }

        if (mothersOwnMotherId == infantId)
        {
            return RuleResult.HardStop(Code, "That patient is linked as this patient's infant; check the MRN.");
        }

        return motherBirthDate is DateOnly born && born >= infantBirthDate
            ? RuleResult.HardStop(Code, $"The mother's date of birth ({born:yyyy-MM-dd}) must be before the infant's ({infantBirthDate:yyyy-MM-dd}); check the MRN.")
            : RuleResult.Pass(Code);
    }
}
