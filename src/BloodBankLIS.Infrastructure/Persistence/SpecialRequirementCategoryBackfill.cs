namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Places special requirement definitions made before categories in a table, keeping what they enforce:
/// product attributes become unit modifications, antigen-negative a unit typing, CMV-negative a unit typing met by the
/// product code or a negative CMV test on the unit, and the rest issue procedures. Unit category stays empty
/// ("All categories"), so current patients keep the same protection.
/// </summary>
public static class SpecialRequirementCategoryBackfill
{
    public static string Sql(string where) => $"""
        UPDATE SpecialRequirementDefinitions
        SET Category = CASE
                WHEN Code = 'CMVNEG' AND EnforcementKind = 0 THEN 0
                WHEN EnforcementKind = 0 THEN 1
                WHEN EnforcementKind = 1 THEN 0
                ELSE 3
            END,
            UnitTestCode = CASE WHEN Code = 'CMVNEG' AND EnforcementKind = 0 THEN 'CMV' ELSE UnitTestCode END,
            EnforcementKind = CASE WHEN Code = 'CMVNEG' AND EnforcementKind = 0 THEN 7 ELSE EnforcementKind END
        WHERE {where}
        """;
}
