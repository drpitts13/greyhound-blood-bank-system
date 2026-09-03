using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Keeps the local SQLite development database aligned with the EF model.
/// Older dev databases were created with <see cref="RelationalDatabaseFacadeExtensions.EnsureCreatedAsync"/>,
/// which does not apply schema updates when the model changes.
/// </summary>
public static class DevelopmentSqliteBootstrap
{
    /// <summary>
    /// Nullable columns added after EnsureCreated. Applied with ALTER TABLE when missing
    /// so local demo data is preserved when possible. SQL is fixed (not user input).
    /// </summary>
    private static readonly (string Table, string Column, string AlterSql)[] AdditiveColumns =
    [
        ("Issues", "Comment", """ALTER TABLE "Issues" ADD COLUMN "Comment" TEXT NULL"""),
        ("Issues", "VerifiedScanJson", """ALTER TABLE "Issues" ADD COLUMN "VerifiedScanJson" TEXT NULL"""),
        ("Issues", "CrossmatchStatus", """ALTER TABLE "Issues" ADD COLUMN "CrossmatchStatus" INTEGER NOT NULL DEFAULT 2"""),
        ("Issues", "EmergencyReleaseDetails", """ALTER TABLE "Issues" ADD COLUMN "EmergencyReleaseDetails" TEXT NULL"""),
        ("Issues", "ReceivedBy", """ALTER TABLE "Issues" ADD COLUMN "ReceivedBy" TEXT NULL"""),
        ("Issues", "UnitExpirationAtIssueUtc", """ALTER TABLE "Issues" ADD COLUMN "UnitExpirationAtIssueUtc" TEXT NULL"""),
        ("Allocations", "AssignmentType", """ALTER TABLE "Allocations" ADD COLUMN "AssignmentType" INTEGER NOT NULL DEFAULT 0"""),
        ("BloodProducts", "ComponentIdentity", """ALTER TABLE "BloodProducts" ADD COLUMN "ComponentIdentity" TEXT NULL"""),
        ("BloodProducts", "ComponentIdentityKey", """ALTER TABLE "BloodProducts" ADD COLUMN "ComponentIdentityKey" TEXT NULL"""),
        ("BloodProducts", "Din", """ALTER TABLE "BloodProducts" ADD COLUMN "Din" TEXT NULL"""),
        ("BloodProducts", "Fin", """ALTER TABLE "BloodProducts" ADD COLUMN "Fin" TEXT NULL"""),
        ("BloodProducts", "NominalYear", """ALTER TABLE "BloodProducts" ADD COLUMN "NominalYear" TEXT NULL"""),
        ("BloodProducts", "DonationSequence", """ALTER TABLE "BloodProducts" ADD COLUMN "DonationSequence" TEXT NULL"""),
        ("BloodProducts", "DinFlags", """ALTER TABLE "BloodProducts" ADD COLUMN "DinFlags" TEXT NULL"""),
        ("BloodProducts", "DinKeyboardCheck", """ALTER TABLE "BloodProducts" ADD COLUMN "DinKeyboardCheck" TEXT NULL"""),
        ("BloodProducts", "AboRhdCode", """ALTER TABLE "BloodProducts" ADD COLUMN "AboRhdCode" TEXT NULL"""),
        ("BloodProducts", "DonationCollectionCategory", """ALTER TABLE "BloodProducts" ADD COLUMN "DonationCollectionCategory" TEXT NULL"""),
        ("BloodProducts", "EncodedPhenotype", """ALTER TABLE "BloodProducts" ADD COLUMN "EncodedPhenotype" TEXT NULL"""),
        ("BloodProducts", "AboSpecialMessage", """ALTER TABLE "BloodProducts" ADD COLUMN "AboSpecialMessage" TEXT NULL"""),
        ("BloodProducts", "ProductCodeData", """ALTER TABLE "BloodProducts" ADD COLUMN "ProductCodeData" TEXT NULL"""),
        ("BloodProducts", "ProductDescriptionCode", """ALTER TABLE "BloodProducts" ADD COLUMN "ProductDescriptionCode" TEXT NULL"""),
        ("BloodProducts", "CollectionTypeCode", """ALTER TABLE "BloodProducts" ADD COLUMN "CollectionTypeCode" TEXT NULL"""),
        ("BloodProducts", "DivisionCode", """ALTER TABLE "BloodProducts" ADD COLUMN "DivisionCode" TEXT NULL"""),
        ("BloodProducts", "ExtendedDivisionCode", """ALTER TABLE "BloodProducts" ADD COLUMN "ExtendedDivisionCode" TEXT NULL"""),
        ("BloodProducts", "ExpirationEncoded", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpirationEncoded" TEXT NULL"""),
        ("BloodProducts", "ExpirationLocal", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpirationLocal" TEXT NULL"""),
        ("BloodProducts", "ExpirationTimezone", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpirationTimezone" TEXT NULL"""),
        ("BloodProducts", "ExpirationHasExplicitTime", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpirationHasExplicitTime" INTEGER NOT NULL DEFAULT 0"""),
        ("BloodProducts", "CollectionDateTime", """ALTER TABLE "BloodProducts" ADD COLUMN "CollectionDateTime" TEXT NULL"""),
        ("BloodProducts", "ProcessingFacilityCode", """ALTER TABLE "BloodProducts" ADD COLUMN "ProcessingFacilityCode" TEXT NULL"""),
        ("BloodProducts", "StandardVersion", """ALTER TABLE "BloodProducts" ADD COLUMN "StandardVersion" TEXT NOT NULL DEFAULT 'PLACEHOLDER-REQUIRES-ICCBBA'"""),
        ("BloodProducts", "Source", """ALTER TABLE "BloodProducts" ADD COLUMN "Source" INTEGER NOT NULL DEFAULT 1"""),
        ("BloodProducts", "ShipmentId", """ALTER TABLE "BloodProducts" ADD COLUMN "ShipmentId" TEXT NULL"""),
        ("BloodProducts", "RecallReason", """ALTER TABLE "BloodProducts" ADD COLUMN "RecallReason" TEXT NULL"""),
        ("BloodProducts", "ReceiveVisualAcceptable", """ALTER TABLE "BloodProducts" ADD COLUMN "ReceiveVisualAcceptable" INTEGER NOT NULL DEFAULT 1"""),
        ("BloodProducts", "ReceiveVisualNotes", """ALTER TABLE "BloodProducts" ADD COLUMN "ReceiveVisualNotes" TEXT NULL"""),
        ("BloodProducts", "ReceiveAppearance", """ALTER TABLE "BloodProducts" ADD COLUMN "ReceiveAppearance" INTEGER NOT NULL DEFAULT 0"""),
        ("BloodProducts", "ReceiveTemperatureCelsius", """ALTER TABLE "BloodProducts" ADD COLUMN "ReceiveTemperatureCelsius" TEXT NULL"""),
        ("BloodProducts", "MissingReason", """ALTER TABLE "BloodProducts" ADD COLUMN "MissingReason" TEXT NULL"""),
        ("BloodProducts", "DamagedReason", """ALTER TABLE "BloodProducts" ADD COLUMN "DamagedReason" TEXT NULL"""),
        ("BloodProducts", "SupplierReturnReason", """ALTER TABLE "BloodProducts" ADD COLUMN "SupplierReturnReason" TEXT NULL"""),
        ("BloodProducts", "DonationRestriction", """ALTER TABLE "BloodProducts" ADD COLUMN "DonationRestriction" INTEGER NOT NULL DEFAULT 0"""),
        ("BloodProducts", "ReservedPatientId", """ALTER TABLE "BloodProducts" ADD COLUMN "ReservedPatientId" INTEGER NULL"""),
        ("BloodProducts", "DirectedConversionReason", """ALTER TABLE "BloodProducts" ADD COLUMN "DirectedConversionReason" TEXT NULL"""),
        ("BloodProducts", "DirectedConvertedUtc", """ALTER TABLE "BloodProducts" ADD COLUMN "DirectedConvertedUtc" TEXT NULL"""),
        ("BloodProducts", "DirectedConvertedBy", """ALTER TABLE "BloodProducts" ADD COLUMN "DirectedConvertedBy" TEXT NULL"""),
        ("Crossmatches", "Phase", """ALTER TABLE "Crossmatches" ADD COLUMN "Phase" TEXT NULL"""),
        ("Crossmatches", "Interpretation", """ALTER TABLE "Crossmatches" ADD COLUMN "Interpretation" TEXT NULL"""),
        ("Crossmatches", "ObservedResultsJson", """ALTER TABLE "Crossmatches" ADD COLUMN "ObservedResultsJson" TEXT NULL"""),
        ("Crossmatches", "ClinicalStatus", """ALTER TABLE "Crossmatches" ADD COLUMN "ClinicalStatus" INTEGER NOT NULL DEFAULT 2"""),
        ("Crossmatches", "RulesVersion", """ALTER TABLE "Crossmatches" ADD COLUMN "RulesVersion" TEXT NULL"""),
        ("Crossmatches", "PolicyVersion", """ALTER TABLE "Crossmatches" ADD COLUMN "PolicyVersion" TEXT NULL"""),
        ("Crossmatches", "EncounterId", """ALTER TABLE "Crossmatches" ADD COLUMN "EncounterId" INTEGER NULL"""),
        ("Crossmatches", "OrderId", """ALTER TABLE "Crossmatches" ADD COLUMN "OrderId" INTEGER NULL"""),
        ("TransfusionEvents", "SecondVerifier", """ALTER TABLE "TransfusionEvents" ADD COLUMN "SecondVerifier" TEXT NULL"""),
        ("TransfusionEvents", "Location", """ALTER TABLE "TransfusionEvents" ADD COLUMN "Location" TEXT NULL"""),
        ("TransfusionEvents", "PreTransfusionVitalsJson", """ALTER TABLE "TransfusionEvents" ADD COLUMN "PreTransfusionVitalsJson" TEXT NULL"""),
        ("TransfusionEvents", "PostTransfusionObservations", """ALTER TABLE "TransfusionEvents" ADD COLUMN "PostTransfusionObservations" TEXT NULL"""),
        ("TransfusionEvents", "PatientIdentificationMethod", """ALTER TABLE "TransfusionEvents" ADD COLUMN "PatientIdentificationMethod" TEXT NULL"""),
        ("TransfusionEvents", "UnitIdentificationMethod", """ALTER TABLE "TransfusionEvents" ADD COLUMN "UnitIdentificationMethod" TEXT NULL"""),
        ("TransfusionEvents", "DeviceId", """ALTER TABLE "TransfusionEvents" ADD COLUMN "DeviceId" TEXT NULL"""),
        ("TransfusionEvents", "WorkstationId", """ALTER TABLE "TransfusionEvents" ADD COLUMN "WorkstationId" TEXT NULL"""),
        ("TransfusionEvents", "BedsideScanVerificationJson", """ALTER TABLE "TransfusionEvents" ADD COLUMN "BedsideScanVerificationJson" TEXT NULL"""),
        ("TransfusionEvents", "RemainderDisposition", """ALTER TABLE "TransfusionEvents" ADD COLUMN "RemainderDisposition" TEXT NULL"""),
        ("TransfusionEvents", "ReactionActions", """ALTER TABLE "TransfusionEvents" ADD COLUMN "ReactionActions" TEXT NULL"""),
        ("TransfusionEvents", "OverrideDataJson", """ALTER TABLE "TransfusionEvents" ADD COLUMN "OverrideDataJson" TEXT NULL"""),
        ("Patients", "RecentPregnancyUtc", """ALTER TABLE "Patients" ADD COLUMN "RecentPregnancyUtc" TEXT NULL"""),
        ("Specimens", "Identifier1Type", """ALTER TABLE "Specimens" ADD COLUMN "Identifier1Type" INTEGER NULL"""),
        ("Specimens", "Identifier1Value", """ALTER TABLE "Specimens" ADD COLUMN "Identifier1Value" TEXT NULL"""),
        ("Specimens", "Identifier2Type", """ALTER TABLE "Specimens" ADD COLUMN "Identifier2Type" INTEGER NULL"""),
        ("Specimens", "Identifier2Value", """ALTER TABLE "Specimens" ADD COLUMN "Identifier2Value" TEXT NULL"""),
        ("Issues", "TestsIncompleteAtIssue", """ALTER TABLE "Issues" ADD COLUMN "TestsIncompleteAtIssue" INTEGER NOT NULL DEFAULT 0"""),
        ("Issues", "VisualInspectionAcceptable", """ALTER TABLE "Issues" ADD COLUMN "VisualInspectionAcceptable" INTEGER NOT NULL DEFAULT 1"""),
        ("Issues", "WardReceivedUtc", """ALTER TABLE "Issues" ADD COLUMN "WardReceivedUtc" TEXT NULL"""),
        ("Issues", "WardReceivedBy", """ALTER TABLE "Issues" ADD COLUMN "WardReceivedBy" TEXT NULL"""),
        ("Issues", "WardVisualAcceptable", """ALTER TABLE "Issues" ADD COLUMN "WardVisualAcceptable" INTEGER NOT NULL DEFAULT 1"""),
        ("Issues", "RetrospectiveCrossmatchDueUtc", """ALTER TABLE "Issues" ADD COLUMN "RetrospectiveCrossmatchDueUtc" TEXT NULL"""),
        ("Issues", "RetrospectiveCrossmatchCompletedUtc", """ALTER TABLE "Issues" ADD COLUMN "RetrospectiveCrossmatchCompletedUtc" TEXT NULL"""),
        ("Issues", "RetrospectiveCrossmatchId", """ALTER TABLE "Issues" ADD COLUMN "RetrospectiveCrossmatchId" INTEGER NULL"""),
        ("Issues", "SecondVerifier", """ALTER TABLE "Issues" ADD COLUMN "SecondVerifier" TEXT NULL"""),
        ("Issues", "PatientIdentifier1", """ALTER TABLE "Issues" ADD COLUMN "PatientIdentifier1" TEXT NULL"""),
        ("Issues", "PatientIdentifier2", """ALTER TABLE "Issues" ADD COLUMN "PatientIdentifier2" TEXT NULL"""),
        ("Issues", "CoolerId", """ALTER TABLE "Issues" ADD COLUMN "CoolerId" TEXT NULL"""),
        ("Issues", "InTransitDueUtc", """ALTER TABLE "Issues" ADD COLUMN "InTransitDueUtc" TEXT NULL"""),
        ("Issues", "WardScanJson", """ALTER TABLE "Issues" ADD COLUMN "WardScanJson" TEXT NULL"""),
        ("Users", "FailedSignInCount", """ALTER TABLE "Users" ADD COLUMN "FailedSignInCount" INTEGER NOT NULL DEFAULT 0"""),
        ("Users", "PinHash", """ALTER TABLE "Users" ADD COLUMN "PinHash" TEXT NULL"""),
        ("ElectronicSignatures", "AuthenticationMethod", """ALTER TABLE "ElectronicSignatures" ADD COLUMN "AuthenticationMethod" INTEGER NOT NULL DEFAULT 2"""),
        ("ElectronicSignatures", "SignatureHash", """ALTER TABLE "ElectronicSignatures" ADD COLUMN "SignatureHash" TEXT NULL"""),
        ("ElectronicSignatures", "ExpiresUtc", """ALTER TABLE "ElectronicSignatures" ADD COLUMN "ExpiresUtc" TEXT NULL"""),
        ("ElectronicSignatures", "ConsumedUtc", """ALTER TABLE "ElectronicSignatures" ADD COLUMN "ConsumedUtc" TEXT NULL"""),
        ("ModificationRules", "ExpirationModificationCodeId", """ALTER TABLE "ModificationRules" ADD COLUMN "ExpirationModificationCodeId" INTEGER NOT NULL DEFAULT 0"""),
        ("ModificationRules", "ModificationCode", """ALTER TABLE "ModificationRules" ADD COLUMN "ModificationCode" TEXT NOT NULL DEFAULT ''"""),
        ("ProductTypes", "RequiresRetype", """ALTER TABLE "ProductTypes" ADD COLUMN "RequiresRetype" INTEGER NOT NULL DEFAULT 0""")
    ];

    public static async Task InitializeAsync(
        BloodBankDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var missing = await FindMissingTableAsync(context, cancellationToken)
            ?? await FindMissingRequiredColumnAsync(context, cancellationToken);
        var recreate = missing is not null;
        if (recreate)
        {
            logger.LogWarning(
                "SQLite development database is missing {Missing}, so its schema is out of date. " +
                "Recreating from the current EF model. Demo data will be re-seeded on startup.",
                missing);
            await context.Database.EnsureDeletedAsync(cancellationToken);
        }

        var created = await context.Database.EnsureCreatedAsync(cancellationToken);
        if (!created && !recreate)
        {
            await ApplyAdditiveColumnsAsync(context, logger, cancellationToken);
        }

        logger.LogInformation(
            recreate || created
                ? "SQLite development database created from EF model."
                : "SQLite development database is up to date.");
    }

    private static async Task ApplyAdditiveColumnsAsync(
        BloodBankDbContext context,
        ILogger logger,
        CancellationToken ct)
    {
        foreach (var (table, column, alterSql) in AdditiveColumns)
        {
            if (!await TableExistsAsync(context, table, ct))
            {
                continue;
            }

            if (await ColumnExistsAsync(context, table, column, ct))
            {
                continue;
            }

            logger.LogWarning(
                "SQLite development database is missing {Table}.{Column}. Adding column via ALTER TABLE.",
                table,
                column);

            await context.Database.ExecuteSqlRawAsync(alterSql, ct);
        }

        await BackfillModificationCodesAsync(context, ct);
    }

    private static async Task BackfillModificationCodesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await TableExistsAsync(context, "ModificationRules", ct)
            || !await ColumnExistsAsync(context, "ModificationRules", "ModificationCode", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE ModificationRules
            SET ModificationCode = CASE ModificationType
                WHEN 0 THEN 'DIV-'
                WHEN 1 THEN 'POOL-'
                WHEN 2 THEN 'IRR-'
                WHEN 3 THEN 'THAW-'
                WHEN 4 THEN 'VR-'
                WHEN 5 THEN 'LR-'
                WHEN 6 THEN 'WASH-'
                ELSE 'MOD-'
            END || COALESCE((SELECT ProductCode FROM ProductTypes WHERE Id = ModificationRules.SourceProductTypeId), CAST(Id AS TEXT))
            WHERE TRIM(ModificationCode) = ''
            """,
            ct);
    }

    /// <summary>
    /// Returns the first table the EF model maps that is absent from the database, or null when
    /// the file is absent, brand new, or already complete. The expected set is read from the model
    /// rather than hand-maintained so a newly added entity cannot silently drift out of the dev
    /// database and fail later during seeding.
    /// </summary>
    private static async Task<string?> FindMissingTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct))
        {
            return null;
        }

        // An empty file is handled by EnsureCreated, not by a recreate.
        if (!await TableExistsAsync(context, "Patients", ct))
        {
            return null;
        }

        var mappedTables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => !string.IsNullOrWhiteSpace(table))
            .Select(table => table!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var table in mappedTables)
        {
            if (!await TableExistsAsync(context, table, ct))
            {
                return table;
            }
        }

        return null;
    }

    /// <summary>
    /// EnsureCreated does not rewrite existing tables. A required column added after the
    /// file was first created (for example TestServiceBillings.ChargeCodeId) must force a
    /// recreate; ALTER TABLE cannot introduce a non-null FK cleanly on populated rows.
    /// </summary>
    private static async Task<string?> FindMissingRequiredColumnAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct) || !await TableExistsAsync(context, "Patients", ct))
        {
            return null;
        }

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(table) || !await TableExistsAsync(context, table, ct))
            {
                continue;
            }

            var existing = await GetColumnNamesAsync(context, table, ct);
            var storeObject = StoreObjectIdentifier.Table(table, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                if (property.IsColumnNullable())
                {
                    continue;
                }

                var column = property.GetColumnName(storeObject);
                if (string.IsNullOrWhiteSpace(column))
                {
                    continue;
                }

                if (!existing.Contains(column))
                {
                    return $"{table}.{column}";
                }
            }
        }

        return null;
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(
        BloodBankDbContext context,
        string tableName,
        CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                names.Add(reader.GetString(1));
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }

        return names;
    }

    private static async Task<bool> TableExistsAsync(
        BloodBankDbContext context,
        string tableName,
        CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table' AND name = $name
                """;
            var param = command.CreateParameter();
            param.ParameterName = "$name";
            param.Value = tableName;
            command.Parameters.Add(param);

            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result) > 0;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        BloodBankDbContext context,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            // PRAGMA table_info does not accept bound parameters for the table name.
            command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
