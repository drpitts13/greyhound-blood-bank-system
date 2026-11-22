namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The hemovigilance report of a transfusion reaction (end-user testing pass 86): the date it was reported cannot be
/// after today, and a reference names the program and its number. Whether and when to report is the facility's.
/// </summary>
public static class ReactionHemovigilanceRule
{
    public const string Code = "RXN-HEMOVIGILANCE";
    public const int MaxReferenceLength = 200;

    public static RuleResult Evaluate(DateOnly? reportedOn, string? reference, DateOnly today)
    {
        if (reportedOn is DateOnly on && on > today)
        {
            return RuleResult.HardStop(Code, "The hemovigilance report date cannot be after today.");
        }

        return reference is not null && reference.Trim().Length > MaxReferenceLength
            ? RuleResult.HardStop(Code, $"The hemovigilance reference can be at most {MaxReferenceLength} characters.")
            : RuleResult.Pass(Code);
    }

    public static string Label(BloodBankLIS.Domain.Enums.ReactionImputability imputability) => imputability switch
    {
        BloodBankLIS.Domain.Enums.ReactionImputability.RuledOut => "Ruled out",
        BloodBankLIS.Domain.Enums.ReactionImputability.NotDetermined => "Not determined",
        _ => imputability.ToString()
    };
}
