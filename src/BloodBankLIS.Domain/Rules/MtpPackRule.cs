using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Validation and round progress for facility MTP packs (OCD-012). Progress is guidance only: it never blocks an
/// issue. Units sent and not returned count toward a round; a returned unit did not reach the patient.
/// </summary>
public static class MtpPackRule
{
    public const string Code = "MTP-PACK";
    public const int MaxUnitsPerComponent = 50;

    public sealed record ComponentProgress(ComponentClass ComponentClass, int PerRound, int Sent, int InCurrentRound);

    public sealed record RoundProgress(int CompletedRounds, int CurrentRound, IReadOnlyList<ComponentProgress> Components);

    public static RuleEvaluation Validate(MtpPackDefinition p, bool duplicateCode)
    {
        var results = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(p.Code))
        {
            results.Add(RuleResult.HardStop(Code, "Enter a code for the pack."));
        }
        else if (p.Code.Trim().Length > 20)
        {
            results.Add(RuleResult.HardStop(Code, "The pack code can be at most 20 characters."));
        }
        else if (duplicateCode)
        {
            results.Add(RuleResult.HardStop(Code, $"Another pack already uses the code {p.Code.Trim()}."));
        }

        if (string.IsNullOrWhiteSpace(p.Name))
        {
            results.Add(RuleResult.HardStop(Code, "Enter a name for the pack."));
        }

        var quantities = Quantities(p);
        if (quantities.Any(q => q.PerRound is < 0 or > MaxUnitsPerComponent))
        {
            results.Add(RuleResult.HardStop(Code, $"Each component must be between 0 and {MaxUnitsPerComponent} units per round."));
        }
        else if (quantities.All(q => q.PerRound == 0))
        {
            results.Add(RuleResult.HardStop(Code, "A pack needs at least one unit of some component."));
        }

        if (results.Count == 0)
        {
            results.Add(RuleResult.Pass(Code));
        }

        return new RuleEvaluation(results);
    }

    public static IReadOnlyList<(ComponentClass ComponentClass, int PerRound)> Quantities(MtpPackDefinition p) =>
    [
        (ComponentClass.RedBloodCells, p.RedCellUnits),
        (ComponentClass.Plasma, p.PlasmaUnits),
        (ComponentClass.Platelets, p.PlateletUnits),
        (ComponentClass.Cryoprecipitate, p.CryoUnits),
        (ComponentClass.WholeBlood, p.WholeBloodUnits)
    ];

    /// <summary>
    /// Rounds completed = the fewest whole rounds any pack component has reached. For each component in the pack,
    /// "in current round" is what it has sent beyond those completed rounds (it can exceed the per-round amount
    /// when one component runs ahead of the others).
    /// </summary>
    public static RoundProgress Progress(MtpPackDefinition p, IReadOnlyDictionary<ComponentClass, int> sentByClass)
    {
        var inPack = Quantities(p).Where(q => q.PerRound > 0).ToList();
        int Sent(ComponentClass c) => sentByClass.TryGetValue(c, out var n) ? Math.Max(0, n) : 0;
        var completed = inPack.Count == 0 ? 0 : inPack.Min(q => Sent(q.ComponentClass) / q.PerRound);
        var components = inPack
            .Select(q => new ComponentProgress(q.ComponentClass, q.PerRound, Sent(q.ComponentClass), Sent(q.ComponentClass) - (completed * q.PerRound)))
            .ToList();
        return new RoundProgress(completed, completed + 1, components);
    }
}
