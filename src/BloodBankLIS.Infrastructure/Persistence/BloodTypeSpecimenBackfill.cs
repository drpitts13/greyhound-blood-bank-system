namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Fills <c>PatientBloodTypeHistory.SpecimenId</c> from the source result for rows recorded before the column
/// existed. Idempotent; the same statement runs in the migration and in the development SQLite bootstrap.
/// </summary>
public static class BloodTypeSpecimenBackfill
{
    public const string Sql = """
        UPDATE PatientBloodTypeHistory
        SET SpecimenId = (SELECT r.SpecimenId FROM TestResults r WHERE r.Id = PatientBloodTypeHistory.SourceResultId)
        WHERE SpecimenId IS NULL AND SourceResultId IS NOT NULL
        """;
}
