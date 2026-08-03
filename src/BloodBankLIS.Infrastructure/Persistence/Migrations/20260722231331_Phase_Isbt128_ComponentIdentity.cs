using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_Isbt128_ComponentIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BedsideScanVerificationJson",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverrideDataJson",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientIdentificationMethod",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostTransfusionObservations",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreTransfusionVitalsJson",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReactionActions",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemainderDisposition",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondVerifier",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitIdentificationMethod",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkstationId",
                table: "TransfusionEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CrossmatchStatus",
                table: "Issues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyReleaseDetails",
                table: "Issues",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "Issues",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnitExpirationAtIssueUtc",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedScanJson",
                table: "Issues",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClinicalStatus",
                table: "Crossmatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "EncounterId",
                table: "Crossmatches",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Interpretation",
                table: "Crossmatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservedResultsJson",
                table: "Crossmatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OrderId",
                table: "Crossmatches",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "Crossmatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "Crossmatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RulesVersion",
                table: "Crossmatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UnitNumber",
                table: "BloodProducts",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "AboRhdCode",
                table: "BloodProducts",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AboSpecialMessage",
                table: "BloodProducts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectionDateTime",
                table: "BloodProducts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionTypeCode",
                table: "BloodProducts",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComponentIdentity",
                table: "BloodProducts",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComponentIdentityKey",
                table: "BloodProducts",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Din",
                table: "BloodProducts",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DinFlags",
                table: "BloodProducts",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DinKeyboardCheck",
                table: "BloodProducts",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DivisionCode",
                table: "BloodProducts",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DonationCollectionCategory",
                table: "BloodProducts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DonationSequence",
                table: "BloodProducts",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncodedPhenotype",
                table: "BloodProducts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpirationEncoded",
                table: "BloodProducts",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExpirationHasExplicitTime",
                table: "BloodProducts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationLocal",
                table: "BloodProducts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpirationTimezone",
                table: "BloodProducts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtendedDivisionCode",
                table: "BloodProducts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fin",
                table: "BloodProducts",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NominalYear",
                table: "BloodProducts",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingFacilityCode",
                table: "BloodProducts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductCodeData",
                table: "BloodProducts",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductDescriptionCode",
                table: "BloodProducts",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecallReason",
                table: "BloodProducts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipmentId",
                table: "BloodProducts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "BloodProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StandardVersion",
                table: "BloodProducts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AssignmentType",
                table: "Allocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BloodComponentCompatibilityDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Pathway = table.Column<int>(type: "int", nullable: false),
                    SatisfiedRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HardStopsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredApprovalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RulesVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodComponentCompatibilityDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodComponentCompatibilityDecisions_BloodProducts_BloodProductId",
                        column: x => x.BloodProductId,
                        principalTable: "BloodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodComponentExceptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    ExceptionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OverrideCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApproverId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodComponentExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodComponentExceptions_BloodProducts_BloodProductId",
                        column: x => x.BloodProductId,
                        principalTable: "BloodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodComponentIdentityCorrections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OriginalValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrectedValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CorrectedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApproverId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SupportingEvidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AffectedTransactionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevalidationRequired = table.Column<bool>(type: "bit", nullable: false),
                    RevalidationCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodComponentIdentityCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodComponentIdentityCorrections_BloodProducts_BloodProductId",
                        column: x => x.BloodProductId,
                        principalTable: "BloodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodComponentRawScans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    StructureKind = table.Column<int>(type: "int", nullable: false),
                    OriginalValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SanitizedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NormalizedValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    EnteredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodComponentRawScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodComponentRawScans_BloodProducts_BloodProductId",
                        column: x => x.BloodProductId,
                        principalTable: "BloodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodComponentScanSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedStructuresJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReceivedStructuresJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastScanAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    StartedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompletedComponentIdentity = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodComponentScanSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodComponentSpecialTests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    TestCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StandardVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodComponentSpecialTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodComponentSpecialTests_BloodProducts_BloodProductId",
                        column: x => x.BloodProductId,
                        principalTable: "BloodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompatibilityRuleVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RetiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompatibilityRuleVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsbtAboRhdCodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Abo = table.Column<int>(type: "int", nullable: false),
                    RhD = table.Column<int>(type: "int", nullable: false),
                    CollectionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SpecialMessage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalPhenotype = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RetiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StandardVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPlaceholder = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsbtAboRhdCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsbtCollectionTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RetiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StandardVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPlaceholder = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsbtCollectionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsbtDataStructures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataIdentifier = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StandardVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsbtDataStructures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsbtProductCodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductDescriptionCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ComponentClass = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Modifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AttributesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StorageRequirements = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequiresExtendedDivision = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RetiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StandardVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPlaceholder = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsbtProductCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodComponentScanSessionLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScanSessionId = table.Column<long>(type: "bigint", nullable: false),
                    StructureKind = table.Column<int>(type: "int", nullable: false),
                    OriginalValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SanitizedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WasDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    WasConflict = table.Column<bool>(type: "bit", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodComponentScanSessionLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodComponentScanSessionLines_BloodComponentScanSessions_ScanSessionId",
                        column: x => x.ScanSessionId,
                        principalTable: "BloodComponentScanSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompatibilityRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompatibilityRuleVersionId = table.Column<long>(type: "bigint", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ComponentClass = table.Column<int>(type: "int", nullable: false),
                    RuleFamily = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpressionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompatibilityRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompatibilityRules_CompatibilityRuleVersions_CompatibilityRuleVersionId",
                        column: x => x.CompatibilityRuleVersionId,
                        principalTable: "CompatibilityRuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloodProducts_ComponentIdentityKey",
                table: "BloodProducts",
                column: "ComponentIdentityKey",
                unique: true,
                filter: "[ComponentIdentityKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BloodProducts_Din",
                table: "BloodProducts",
                column: "Din");

            migrationBuilder.CreateIndex(
                name: "IX_BloodComponentCompatibilityDecisions_BloodProductId_PatientId_EvaluatedAt",
                table: "BloodComponentCompatibilityDecisions",
                columns: new[] { "BloodProductId", "PatientId", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodComponentExceptions_BloodProductId",
                table: "BloodComponentExceptions",
                column: "BloodProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodComponentIdentityCorrections_BloodProductId",
                table: "BloodComponentIdentityCorrections",
                column: "BloodProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodComponentRawScans_BloodProductId",
                table: "BloodComponentRawScans",
                column: "BloodProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodComponentScanSessionLines_ScanSessionId",
                table: "BloodComponentScanSessionLines",
                column: "ScanSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodComponentScanSessions_SessionKey",
                table: "BloodComponentScanSessions",
                column: "SessionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodComponentSpecialTests_BloodProductId",
                table: "BloodComponentSpecialTests",
                column: "BloodProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CompatibilityRules_CompatibilityRuleVersionId_RuleCode",
                table: "CompatibilityRules",
                columns: new[] { "CompatibilityRuleVersionId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompatibilityRuleVersions_Version",
                table: "CompatibilityRuleVersions",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsbtAboRhdCodes_Code_StandardVersion",
                table: "IsbtAboRhdCodes",
                columns: new[] { "Code", "StandardVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsbtCollectionTypes_Code",
                table: "IsbtCollectionTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsbtDataStructures_DataIdentifier",
                table: "IsbtDataStructures",
                column: "DataIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsbtProductCodes_ProductDescriptionCode_StandardVersion",
                table: "IsbtProductCodes",
                columns: new[] { "ProductDescriptionCode", "StandardVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BloodComponentCompatibilityDecisions");

            migrationBuilder.DropTable(
                name: "BloodComponentExceptions");

            migrationBuilder.DropTable(
                name: "BloodComponentIdentityCorrections");

            migrationBuilder.DropTable(
                name: "BloodComponentRawScans");

            migrationBuilder.DropTable(
                name: "BloodComponentScanSessionLines");

            migrationBuilder.DropTable(
                name: "BloodComponentSpecialTests");

            migrationBuilder.DropTable(
                name: "CompatibilityRules");

            migrationBuilder.DropTable(
                name: "IsbtAboRhdCodes");

            migrationBuilder.DropTable(
                name: "IsbtCollectionTypes");

            migrationBuilder.DropTable(
                name: "IsbtDataStructures");

            migrationBuilder.DropTable(
                name: "IsbtProductCodes");

            migrationBuilder.DropTable(
                name: "BloodComponentScanSessions");

            migrationBuilder.DropTable(
                name: "CompatibilityRuleVersions");

            migrationBuilder.DropIndex(
                name: "IX_BloodProducts_ComponentIdentityKey",
                table: "BloodProducts");

            migrationBuilder.DropIndex(
                name: "IX_BloodProducts_Din",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "BedsideScanVerificationJson",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "OverrideDataJson",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "PatientIdentificationMethod",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "PostTransfusionObservations",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "PreTransfusionVitalsJson",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "ReactionActions",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "RemainderDisposition",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "SecondVerifier",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "UnitIdentificationMethod",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "WorkstationId",
                table: "TransfusionEvents");

            migrationBuilder.DropColumn(
                name: "CrossmatchStatus",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "EmergencyReleaseDetails",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "UnitExpirationAtIssueUtc",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "VerifiedScanJson",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "ClinicalStatus",
                table: "Crossmatches");

            migrationBuilder.DropColumn(
                name: "EncounterId",
                table: "Crossmatches");

            migrationBuilder.DropColumn(
                name: "Interpretation",
                table: "Crossmatches");

            migrationBuilder.DropColumn(
                name: "ObservedResultsJson",
                table: "Crossmatches");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Crossmatches");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "Crossmatches");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "Crossmatches");

            migrationBuilder.DropColumn(
                name: "RulesVersion",
                table: "Crossmatches");

            migrationBuilder.DropColumn(
                name: "AboRhdCode",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "AboSpecialMessage",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "CollectionDateTime",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "CollectionTypeCode",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ComponentIdentity",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ComponentIdentityKey",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "Din",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "DinFlags",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "DinKeyboardCheck",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "DivisionCode",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "DonationCollectionCategory",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "DonationSequence",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "EncodedPhenotype",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ExpirationEncoded",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ExpirationHasExplicitTime",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ExpirationLocal",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ExpirationTimezone",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ExtendedDivisionCode",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "Fin",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "NominalYear",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ProcessingFacilityCode",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ProductCodeData",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ProductDescriptionCode",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "RecallReason",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "ShipmentId",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "StandardVersion",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "AssignmentType",
                table: "Allocations");

            migrationBuilder.AlterColumn<string>(
                name: "UnitNumber",
                table: "BloodProducts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);
        }
    }
}
