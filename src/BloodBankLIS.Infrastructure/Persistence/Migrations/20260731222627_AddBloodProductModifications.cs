using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBloodProductModifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DerivedFromModificationId",
                table: "BloodProducts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModificationRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceProductTypeId = table.Column<long>(type: "bigint", nullable: false),
                    ModificationType = table.Column<int>(type: "int", nullable: false),
                    TargetProductTypeId = table.Column<long>(type: "bigint", nullable: false),
                    ExpirationOffsetCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModificationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModificationRules_ProductTypes_SourceProductTypeId",
                        column: x => x.SourceProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModificationRules_ProductTypes_TargetProductTypeId",
                        column: x => x.TargetProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitModifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModificationRuleId = table.Column<long>(type: "bigint", nullable: false),
                    ModificationType = table.Column<int>(type: "int", nullable: false),
                    ExpirationOffsetCodeApplied = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ResultExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PerformedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitModifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitModifications_ModificationRules_ModificationRuleId",
                        column: x => x.ModificationRuleId,
                        principalTable: "ModificationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitModificationUnits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitModificationId = table.Column<long>(type: "bigint", nullable: false),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitModificationUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitModificationUnits_BloodProducts_BloodProductId",
                        column: x => x.BloodProductId,
                        principalTable: "BloodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitModificationUnits_UnitModifications_UnitModificationId",
                        column: x => x.UnitModificationId,
                        principalTable: "UnitModifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloodProducts_DerivedFromModificationId",
                table: "BloodProducts",
                column: "DerivedFromModificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationRules_IsActive",
                table: "ModificationRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationRules_SourceProductTypeId_ModificationType_TargetProductTypeId",
                table: "ModificationRules",
                columns: new[] { "SourceProductTypeId", "ModificationType", "TargetProductTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModificationRules_TargetProductTypeId",
                table: "ModificationRules",
                column: "TargetProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitModifications_ModificationRuleId",
                table: "UnitModifications",
                column: "ModificationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitModifications_PerformedUtc",
                table: "UnitModifications",
                column: "PerformedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UnitModificationUnits_BloodProductId",
                table: "UnitModificationUnits",
                column: "BloodProductId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitModificationUnits_UnitModificationId",
                table: "UnitModificationUnits",
                column: "UnitModificationId");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodProducts_UnitModifications_DerivedFromModificationId",
                table: "BloodProducts",
                column: "DerivedFromModificationId",
                principalTable: "UnitModifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BloodProducts_UnitModifications_DerivedFromModificationId",
                table: "BloodProducts");

            migrationBuilder.DropTable(
                name: "UnitModificationUnits");

            migrationBuilder.DropTable(
                name: "UnitModifications");

            migrationBuilder.DropTable(
                name: "ModificationRules");

            migrationBuilder.DropIndex(
                name: "IX_BloodProducts_DerivedFromModificationId",
                table: "BloodProducts");

            migrationBuilder.DropColumn(
                name: "DerivedFromModificationId",
                table: "BloodProducts");
        }
    }
}
