using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// Resolves a technologist-entered antibody specificity to the blood-attribute
/// catalog so identified antibodies can drive antigen-negative selection.
/// Does not classify or identify antibodies.
/// </summary>
public static class AntibodyIdentificationCatalogResolver
{
    public const string UnmatchedIdentifiedCode = "ABID-INTERP-UNMATCHED";

    public sealed record Resolution(long? DefinitionId, string Specificity, bool CatalogMatched);

    public static Resolution Resolve(
        long? requestedDefinitionId,
        string? specificity,
        IReadOnlyList<AntibodyCatalogItem> catalog)
    {
        var trimmed = (specificity ?? string.Empty).Trim();

        if (requestedDefinitionId is long id)
        {
            var byId = catalog.FirstOrDefault(c => c.Id == id);
            if (byId is not null)
            {
                return new Resolution(byId.Id, byId.AntibodyName, true);
            }
        }

        if (string.IsNullOrWhiteSpace(trimmed) || catalog.Count == 0)
        {
            return new Resolution(null, trimmed, false);
        }

        var hits = AntibodyIdentificationParser.Resolve(trimmed, catalog);
        var matched = hits
            .Select(h => h.CatalogItem)
            .Where(c => c is not null)
            .Cast<AntibodyCatalogItem>()
            .DistinctBy(c => c.Id)
            .ToList();

        return matched.Count == 1
            ? new Resolution(matched[0].Id, matched[0].AntibodyName, true)
            : new Resolution(null, AntibodyIdentificationParser.Format([trimmed]), false);
    }

    public static RuleResult EvaluateIdentifiedCatalog(
        AntibodyIdClassification classification,
        bool catalogMatched,
        string specificity)
    {
        if (classification != AntibodyIdClassification.Identified || catalogMatched)
        {
            return RuleResult.Pass(UnmatchedIdentifiedCode);
        }

        return RuleResult.Warning(
            UnmatchedIdentifiedCode,
            $"{specificity} is not a unique catalog antibody. It will post as free-text history and will not drive antigen-negative selection until a catalog attribute exists. Technologist judgment is required.");
    }
}
