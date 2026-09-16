using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BloodBankLIS.Infrastructure.Persistence;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260914180000_SpecialRequirementCatalog")]
    public partial class SpecialRequirementCatalog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecialRequirementDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    EnforcementKind = table.Column<int>(type: "int", nullable: false),
                    ProductAttributeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Instruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    IsPendingApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialRequirementDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialRequirementDefinitions_Code",
                table: "SpecialRequirementDefinitions",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialRequirementDefinitions_IsActive_IsDraft",
                table: "SpecialRequirementDefinitions",
                columns: new[] { "IsActive", "IsDraft" });

            migrationBuilder.AddColumn<long>(
                name: "RequirementDefinitionId",
                table: "SpecialTransfusionRequirements",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTransfusionRequirements_RequirementDefinitionId",
                table: "SpecialTransfusionRequirements",
                column: "RequirementDefinitionId");

            migrationBuilder.AddColumn<int>(
                name: "AboSubgroup",
                table: "PatientBloodTypeHistory",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedSpecialRequirementCodes",
                table: "Issues",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SpecialTransfusionRequirements_RequirementDefinitionId",
                table: "SpecialTransfusionRequirements");
            migrationBuilder.DropColumn(name: "RequirementDefinitionId", table: "SpecialTransfusionRequirements");
            migrationBuilder.DropColumn(name: "AboSubgroup", table: "PatientBloodTypeHistory");
            migrationBuilder.DropColumn(name: "AcknowledgedSpecialRequirementCodes", table: "Issues");
            migrationBuilder.DropTable(name: "SpecialRequirementDefinitions");
        }
    }
}
