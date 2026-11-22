using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The Reactions board's Workup column (end-user testing pass 162: it repeated the Next step sentence word for word, so
/// the row said the same thing twice and nothing showed how far the workup had come). It now lists the standard workup
/// steps recorded and still to record: clerical check, visual inspection, repeat ABO/Rh of patient and unit, DAT.
/// </summary>
public static class ReactionWorkupProgress
{
    public static (IReadOnlyList<string> Done, IReadOnlyList<string> ToDo) For(ReactionInvestigationDto r)
    {
        var steps = new (string Name, bool Recorded)[]
        {
            ("clerical check", r.ClericalCheckCompleted),
            ("visual inspection", r.VisualInspectionCompleted),
            ("repeat patient ABO/Rh", !string.IsNullOrWhiteSpace(r.RepeatPatientAboRh)),
            ("repeat unit ABO/Rh", !string.IsNullOrWhiteSpace(r.RepeatUnitAboRh)),
            ("DAT", r.DatResult != DatWorkupResult.NotRecorded)
        };
        return (steps.Where(s => s.Recorded).Select(s => s.Name).ToList(), steps.Where(s => !s.Recorded).Select(s => s.Name).ToList());
    }

    /// <summary>"2 of 5 recorded; to do: visual inspection, DAT".</summary>
    public static string Text(ReactionInvestigationDto r)
    {
        var (done, toDo) = For(r);
        var total = done.Count + toDo.Count;
        return toDo.Count == 0
            ? $"All {total} recorded"
            : $"{done.Count} of {total} recorded; to do: {string.Join(", ", toDo)}";
    }
}
