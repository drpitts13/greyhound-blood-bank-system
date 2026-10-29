using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Carries recorded unit antigen or antibody typing onto a modification result.
/// A pool is positive when any source is positive. A negative is copied only when
/// every source was typed negative. Conflicting results stop the modification.
/// </summary>
public static class UnitModificationAttributeRule
{
    public const string DiscordantCode = "MOD-POOL-ATTR-DISCORDANT";

    public readonly record struct AttributeFact(
        long DefinitionId,
        BloodAttributeKind Kind,
        AntigenResult Result,
        UnitAttributeTypingSource TypingSource,
        long? SourceResultId);

    public readonly record struct InheritedAttribute(
        long DefinitionId,
        BloodAttributeKind Kind,
        AntigenResult Result,
        UnitAttributeTypingSource TypingSource,
        long? SourceResultId);

    public sealed record AttributeInheritance(
        RuleEvaluation Evaluation,
        IReadOnlyList<InheritedAttribute> Inherited);

    public static AttributeInheritance Evaluate(IReadOnlyList<IReadOnlyList<AttributeFact>> sources)
    {
        var tested = sources
            .Select(source => source.Where(fact => fact.Result != AntigenResult.NotTested).ToList())
            .ToList();
        var keys = tested
            .SelectMany(source => source.Select(fact => (fact.DefinitionId, fact.Kind)))
            .Distinct()
            .ToList();

        var inherited = new List<InheritedAttribute>();
        var stops = new List<RuleResult>();

        foreach (var key in keys)
        {
            var present = tested
                .SelectMany(source => source.Where(fact =>
                    fact.DefinitionId == key.DefinitionId && fact.Kind == key.Kind))
                .ToList();
            var sourcesWithResult = tested.Count(source => source.Any(fact =>
                fact.DefinitionId == key.DefinitionId && fact.Kind == key.Kind));

            var hasPositive = present.Any(fact => fact.Result == AntigenResult.Positive);
            var hasNegative = present.Any(fact => fact.Result == AntigenResult.Negative);
            if (hasPositive && hasNegative)
            {
                stops.Add(RuleResult.HardStop(
                    DiscordantCode,
                    "Pooled units disagree on an antigen or antibody result. Retype them before pooling."));
                continue;
            }

            if (hasPositive)
            {
                var chosen = present.First(fact => fact.Result == AntigenResult.Positive);
                inherited.Add(ToInherited(chosen));
                continue;
            }

            if (hasNegative && sourcesWithResult == tested.Count)
            {
                inherited.Add(ToInherited(present[0]));
            }
        }

        var evaluation = stops.Count == 0
            ? new RuleEvaluation(new[] { RuleResult.Pass(DiscordantCode) })
            : new RuleEvaluation(stops);
        return new AttributeInheritance(evaluation, inherited);
    }

    private static InheritedAttribute ToInherited(AttributeFact fact) =>
        new(fact.DefinitionId, fact.Kind, fact.Result, fact.TypingSource, fact.SourceResultId);
}
