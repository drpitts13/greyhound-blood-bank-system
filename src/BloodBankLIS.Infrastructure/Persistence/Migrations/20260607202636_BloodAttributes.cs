using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BloodAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BloodAttributeScopeJson",
                table: "TestDefinitions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContributesToUnitBloodAttributes",
                table: "TestDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "BloodAttributeDefinitionId",
                table: "AntibodyHistory",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AntigenProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    BloodAttributeDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TestedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceResultId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntigenProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodAttributeDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AntibodyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsClinicallySignificant = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    IsPendingApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodAttributeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitBloodAttributes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    BloodAttributeDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    AttributeKind = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    SourceResultId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitBloodAttributes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntigenProfiles_PatientId_BloodAttributeDefinitionId",
                table: "AntigenProfiles",
                columns: new[] { "PatientId", "BloodAttributeDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodAttributeDefinitions_Code",
                table: "BloodAttributeDefinitions",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_UnitBloodAttributes_BloodProductId_BloodAttributeDefinitionId_AttributeKind",
                table: "UnitBloodAttributes",
                columns: new[] { "BloodProductId", "BloodAttributeDefinitionId", "AttributeKind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AntigenProfiles");

            migrationBuilder.DropTable(
                name: "BloodAttributeDefinitions");

            migrationBuilder.DropTable(
                name: "UnitBloodAttributes");

            migrationBuilder.DropColumn(
                name: "BloodAttributeScopeJson",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "ContributesToUnitBloodAttributes",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "BloodAttributeDefinitionId",
                table: "AntibodyHistory");
        }
    }
}
