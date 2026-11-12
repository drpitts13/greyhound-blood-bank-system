namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// The note above the unit picker when units are offered for a patient: how many units are compatible, and of which
/// products. End-user testing, 2026-11-12: a patient needing irradiated, CMV-negative red cells was told "6 available
/// units are compatible"; all six were plasma, and nothing said that no red cell qualified.
/// </summary>
public static class CompatibleUnitSummary
{
    public static string Note(IReadOnlyList<CompatibleUnitDto> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        if (units.Count == 0)
        {
            return "No available unit is compatible with this patient's type, special requirements, and antibodies.";
        }

        var head = units.Count == 1
            ? "1 available unit is compatible with this patient's type, special requirements, and antibodies"
            : $"{units.Count} available units are compatible with this patient's type, special requirements, and antibodies";
        var byProduct = units
            .GroupBy(u => string.IsNullOrWhiteSpace(u.ProductName) ? u.ProductCode : u.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Count()} {g.Key}");
        return $"{head}: {string.Join(", ", byProduct)}.";
    }
}
