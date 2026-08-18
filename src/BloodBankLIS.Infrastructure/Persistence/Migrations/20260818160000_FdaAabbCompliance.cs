using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260818160000_FdaAabbCompliance")]
    public partial class FdaAabbCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RecentPregnancyUtc",
                table: "Patients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Identifier1Type",
                table: "Specimens",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier1Value",
                table: "Specimens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Identifier2Type",
                table: "Specimens",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier2Value",
                table: "Specimens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TestsIncompleteAtIssue",
                table: "Issues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VisualInspectionAcceptable",
                table: "Issues",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondVerifier",
                table: "Issues",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientIdentifier1",
                table: "Issues",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientIdentifier2",
                table: "Issues",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedSignInCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AuthenticationMethod",
                table: "ElectronicSignatures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SignatureHash",
                table: "ElectronicSignatures",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresUtc",
                table: "ElectronicSignatures",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsumedUtc",
                table: "ElectronicSignatures",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LegalHold = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecialTransfusionRequirements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    RequirementType = table.Column<int>(type: "int", nullable: false),
                    AntigenCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EffectiveUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EnteredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeactivationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialTransfusionRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecialTransfusionRequirements_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientIdentifiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    IdentifierType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssigningAuthority = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientIdentifiers_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReactionInvestigations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransfusionEventId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    ReportedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReactionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Conclusions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FollowUp = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductAtFault = table.Column<bool>(type: "bit", nullable: false),
                    IsFatality = table.Column<bool>(type: "bit", nullable: false),
                    FatalityNotificationStatus = table.Column<int>(type: "int", nullable: false),
                    WrittenReportDueUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CberNotifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WrittenReportSubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedSignatureId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionInvestigations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactionInvestigations_TransfusionEvents_TransfusionEventId",
                        column: x => x.TransfusionEventId,
                        principalTable: "TransfusionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LookbackNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Din = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    IssueId = table.Column<long>(type: "bigint", nullable: true),
                    TransfusionEventId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PhysicianOfRecord = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AttemptedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookbackNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deviations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ContextType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContextId = table.Column<long>(type: "bigint", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReportedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deviations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTransfusionRequirements_PatientId_IsActive",
                table: "SpecialTransfusionRequirements",
                columns: new[] { "PatientId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifiers_IdentifierType_Value_AssigningAuthority",
                table: "PatientIdentifiers",
                columns: new[] { "IdentifierType", "Value", "AssigningAuthority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifiers_PatientId",
                table: "PatientIdentifiers",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ReactionInvestigations_TransfusionEventId",
                table: "ReactionInvestigations",
                column: "TransfusionEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ReactionInvestigations_PatientId",
                table: "ReactionInvestigations",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LookbackNotifications_Din",
                table: "LookbackNotifications",
                column: "Din");

            migrationBuilder.CreateIndex(
                name: "IX_LookbackNotifications_PatientId",
                table: "LookbackNotifications",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Deviations_Status",
                table: "Deviations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Deviations");
            migrationBuilder.DropTable(name: "LookbackNotifications");
            migrationBuilder.DropTable(name: "ReactionInvestigations");
            migrationBuilder.DropTable(name: "PatientIdentifiers");
            migrationBuilder.DropTable(name: "SpecialTransfusionRequirements");
            migrationBuilder.DropTable(name: "SystemSettings");

            migrationBuilder.DropColumn(name: "RecentPregnancyUtc", table: "Patients");
            migrationBuilder.DropColumn(name: "Identifier1Type", table: "Specimens");
            migrationBuilder.DropColumn(name: "Identifier1Value", table: "Specimens");
            migrationBuilder.DropColumn(name: "Identifier2Type", table: "Specimens");
            migrationBuilder.DropColumn(name: "Identifier2Value", table: "Specimens");
            migrationBuilder.DropColumn(name: "TestsIncompleteAtIssue", table: "Issues");
            migrationBuilder.DropColumn(name: "VisualInspectionAcceptable", table: "Issues");
            migrationBuilder.DropColumn(name: "SecondVerifier", table: "Issues");
            migrationBuilder.DropColumn(name: "PatientIdentifier1", table: "Issues");
            migrationBuilder.DropColumn(name: "PatientIdentifier2", table: "Issues");
            migrationBuilder.DropColumn(name: "FailedSignInCount", table: "Users");
            migrationBuilder.DropColumn(name: "PinHash", table: "Users");
            migrationBuilder.DropColumn(name: "AuthenticationMethod", table: "ElectronicSignatures");
            migrationBuilder.DropColumn(name: "SignatureHash", table: "ElectronicSignatures");
            migrationBuilder.DropColumn(name: "ExpiresUtc", table: "ElectronicSignatures");
            migrationBuilder.DropColumn(name: "ConsumedUtc", table: "ElectronicSignatures");
        }
    }
}
