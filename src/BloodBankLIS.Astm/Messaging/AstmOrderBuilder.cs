using BloodBankLIS.Astm.Parsing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;

namespace BloodBankLIS.Astm.Messaging;

/// <summary>Builds an ImmuLINK order-download message for one specimen query.</summary>
public static class AstmOrderBuilder
{
    public static string Build(
        Patient patient,
        Specimen specimen,
        IReadOnlyList<OrderLine> lines,
        InstrumentValueTranslator translator,
        out IReadOnlyList<string> notSent,
        string? donorUnitNumber = null)
    {
        var missing = new List<string>();
        notSent = missing;
        var now = DateTime.UtcNow;
        var records = new List<string>
        {
            AstmBuilder.Header("LIS", "ImmuLINK", now),
            AstmBuilder.Patient(
                1,
                patient.MedicalRecordNumber,
                patient.LastName,
                patient.FirstName,
                patient.DateOfBirth.ToString("yyyyMMdd"),
                patient.Sex switch { Sex.Male => "M", Sex.Female => "F", _ => "U" })
        };

        var seq = 1;
        foreach (var line in lines.Where(l => l.IsActive && !string.IsNullOrWhiteSpace(l.TestCode)))
        {
            var assay = translator.ToInstrument(InstrumentTranslationKeys.OrderTestCode, line.TestCode!);
            if (string.IsNullOrWhiteSpace(assay))
            {
                // Not every test runs on every instrument; the caller names the ones left out.
                if (!missing.Contains(line.TestCode!, StringComparer.OrdinalIgnoreCase))
                {
                    missing.Add(line.TestCode!);
                }

                continue;
            }

            records.Add(AstmBuilder.Order(seq++, specimen.AccessionNumber, assay, "Blood^Patient", "N"));
            if (string.Equals(assay, "Crossmatch", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(donorUnitNumber))
            {
                records.Add(AstmBuilder.Comment(1, "L", $"Donor^{donorUnitNumber}"));
            }
        }

        records.Add(AstmBuilder.Terminator());
        return AstmBuilder.Join(records.ToArray());
    }
}
