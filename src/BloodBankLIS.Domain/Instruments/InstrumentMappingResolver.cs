using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Instruments;

/// <summary>Resolves instrument ↔ application codes using order context and sort order.</summary>
public static class InstrumentMappingResolver
{
    public static string? ToApplication(
        IEnumerable<InstrumentTestMapping> mappings,
        string instrumentCode,
        InstrumentMappingKind kind,
        IReadOnlyCollection<string>? orderTestCodes = null)
    {
        var matches = mappings
            .Where(m => m.Kind == kind
                && m.Direction is InterfaceTranslationDirection.Inbound or InterfaceTranslationDirection.Both
                && string.Equals(m.InstrumentCode, instrumentCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Id)
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        if (orderTestCodes is { Count: > 0 })
        {
            var onOrder = matches.FirstOrDefault(m =>
                orderTestCodes.Contains(m.ApplicationCode, StringComparer.OrdinalIgnoreCase));
            if (onOrder is not null)
            {
                return onOrder.ApplicationCode;
            }
        }

        return matches[0].ApplicationCode;
    }

    public static string? ToInstrument(
        IEnumerable<InstrumentTestMapping> mappings,
        string applicationCode,
        InstrumentMappingKind kind)
    {
        return mappings
            .Where(m => m.Kind == kind
                && m.Direction is InterfaceTranslationDirection.Outbound or InterfaceTranslationDirection.Both
                && string.Equals(m.ApplicationCode, applicationCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Id)
            .Select(m => m.InstrumentCode)
            .FirstOrDefault();
    }
}
