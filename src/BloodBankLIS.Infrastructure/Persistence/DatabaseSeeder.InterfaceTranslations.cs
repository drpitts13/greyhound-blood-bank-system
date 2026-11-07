using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Starter interface translations, for all interfaces: the type and screen grouper's Epic procedure code
/// (TNS ↔ LAB10117), HL7 patient classes (table 0004), and the CDC race and ethnicity codes (tables 0005 and 0189).
/// A field is seeded only while it has no rows, so a site's own edits and deletions stay as they are. These are
/// standard code sets the interface team confirms against their EHR build; none is a clinical rule.
/// </summary>
public static partial class DatabaseSeeder
{
    private static readonly (string Key, string Local, string External, InterfaceTranslationDirection Direction)[] StarterTranslations =
    [
        (InterfaceDataItemKeys.ProcedureCode, "TNS", "LAB10117", InterfaceTranslationDirection.Both),

        (InterfaceDataItemKeys.EncounterPatientClass, nameof(EncounterType.Inpatient), "I", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.EncounterPatientClass, nameof(EncounterType.Outpatient), "O", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.EncounterPatientClass, nameof(EncounterType.Emergency), "E", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.EncounterPatientClass, nameof(EncounterType.RecurringOutpatient), "R", InterfaceTranslationDirection.Both),
        // HL7 has no observation class of its own here; an inbound O stays Outpatient.
        (InterfaceDataItemKeys.EncounterPatientClass, nameof(EncounterType.Observation), "O", InterfaceTranslationDirection.Outbound),

        (InterfaceDataItemKeys.PatientRace, "AIAN", "1002-5", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.PatientRace, "ASIAN", "2028-9", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.PatientRace, "BLACK", "2054-5", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.PatientRace, "NHPI", "2076-8", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.PatientRace, "WHITE", "2106-3", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.PatientRace, "OTHER", "2131-1", InterfaceTranslationDirection.Both),

        (InterfaceDataItemKeys.PatientEthnicity, "HISPANIC", "2135-2", InterfaceTranslationDirection.Both),
        (InterfaceDataItemKeys.PatientEthnicity, "NOT-HISPANIC", "2186-5", InterfaceTranslationDirection.Both)
    ];

    private static async Task EnsureStarterInterfaceTranslationsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var keysWithRows = await context.InterfaceValueTranslations
            .Select(t => t.DataItemKey)
            .Distinct()
            .ToListAsync(ct);
        var added = false;
        foreach (var group in StarterTranslations.GroupBy(t => t.Key))
        {
            if (keysWithRows.Contains(group.Key))
            {
                continue;
            }

            context.InterfaceValueTranslations.AddRange(group.Select(t => new InterfaceValueTranslation
            {
                DataItemKey = t.Key,
                InternalValue = t.Local,
                ExternalValue = t.External,
                Direction = t.Direction
            }));
            added = true;
        }

        if (added)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Demo data for translations: lines of demo type and screen orders keep the grouper they came from (so their
    /// results go to the EHR as LAB10117), two demo patients have race and ethnicity, and one unmapped inbound order
    /// code is listed. Each is added once.
    /// </summary>
    private static async Task EnsureInterfaceTranslationDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var tnsLines = await context.OrderLines
            .Where(l => l.GrouperCode == null && l.Order != null && l.Order.TestCode == "TNS"
                && (l.TestCode == "ABORH" || l.TestCode == "ABSC"))
            .ToListAsync(ct);
        foreach (var line in tnsLines)
        {
            line.GrouperCode = "TNS";
        }

        if (!await context.Patients.AnyAsync(p => p.Race != null || p.Ethnicity != null, ct))
        {
            var demo = await context.Patients
                .Where(p => p.MedicalRecordNumber == "MRN0001" || p.MedicalRecordNumber == "MRN0002")
                .ToListAsync(ct);
            foreach (var p in demo)
            {
                (p.Race, p.Ethnicity) = p.MedicalRecordNumber == "MRN0001" ? ("WHITE", "NOT-HISPANIC") : ("BLACK", "NOT-HISPANIC");
            }
        }

        if (!await context.InterfaceUnmappedValues.AnyAsync(ct))
        {
            var seen = DateTime.UtcNow.AddHours(-3);
            context.InterfaceUnmappedValues.Add(new InterfaceUnmappedValue
            {
                DataItemKey = InterfaceDataItemKeys.OrderTestCode,
                Value = "LAB99999",
                Direction = InterfaceTranslationDirection.Inbound,
                FirstSeenUtc = seen,
                LastSeenUtc = seen.AddHours(2),
                Count = 2,
                LastMessageControlId = "DEMO-ORM-99999"
            });
        }

        await context.SaveChangesAsync(ct);
    }
}
