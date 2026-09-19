using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Computes a specimen expiration from collection date/time and a catalog offset.
/// End-of-day mode uses 23:59 UTC on the calendar date of the exact result.
/// Hours always expire at the exact instant even if end-of-day is requested.
/// </summary>
public static class SpecimenExpirationCalculator
{
    public static DateTime ComputeExpiresUtc(
        DateTime collectedUtc,
        SpecimenExpirationCode code,
        SpecimenExpirationMode mode)
    {
        var exact = AddOffset(collectedUtc, code);
        if (mode == SpecimenExpirationMode.ExactTime || code.Unit == SpecimenExpirationUnit.Hours)
        {
            return DateTime.SpecifyKind(exact, DateTimeKind.Utc);
        }

        return new DateTime(exact.Year, exact.Month, exact.Day, 23, 59, 0, DateTimeKind.Utc);
    }

    public static DateTime ComputeExpiresUtc(
        DateTime collectedUtc,
        string? expirationCode,
        SpecimenExpirationMode mode)
    {
        if (!SpecimenExpirationCode.TryParse(expirationCode, out var code))
        {
            throw new ArgumentException(
                $"Specimen expiration code '{expirationCode}' is not a valid H/D/W/M offset.",
                nameof(expirationCode));
        }

        return ComputeExpiresUtc(collectedUtc, code, mode);
    }

    private static DateTime AddOffset(DateTime collectedUtc, SpecimenExpirationCode code) =>
        code.Unit switch
        {
            SpecimenExpirationUnit.Hours => collectedUtc.AddHours(code.Amount),
            SpecimenExpirationUnit.Days => collectedUtc.AddDays(code.Amount),
            SpecimenExpirationUnit.Weeks => collectedUtc.AddDays(code.Amount * 7L),
            SpecimenExpirationUnit.Months => collectedUtc.AddMonths(code.Amount),
            _ => throw new InvalidOperationException($"Unhandled {nameof(SpecimenExpirationUnit)}: {code.Unit}")
        };
}
