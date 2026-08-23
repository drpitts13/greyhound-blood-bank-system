using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260823000000_ExpirationModificationCodes")]
    public partial class ExpirationModificationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpirationModificationCodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OffsetAmount = table.Column<int>(type: "int", nullable: false),
                    OffsetUnit = table.Column<int>(type: "int", nullable: false),
                    RelativeTo = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ExpirationModificationCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpirationModificationCodes_Code",
                table: "ExpirationModificationCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpirationModificationCodes_IsActive",
                table: "ExpirationModificationCodes",
                column: "IsActive");

            migrationBuilder.Sql("""
                INSERT INTO ExpirationModificationCodes (Code, OffsetAmount, OffsetUnit, RelativeTo, Description, IsActive, Version, CreatedUtc, CreatedBy)
                VALUES
                (N'24H', 24, 0, 0, N'24 hours from modification', 1, 1, SYSUTCDATETIME(), N'system'),
                (N'5D', 5, 1, 0, N'5 days from modification', 1, 1, SYSUTCDATETIME(), N'system'),
                (N'28D', 28, 1, 0, N'28 days from modification', 1, 1, SYSUTCDATETIME(), N'system'),
                (N'42D', 42, 1, 1, N'42 days from collection', 1, 1, SYSUTCDATETIME(), N'system');
                """);

            migrationBuilder.AddColumn<long>(
                name: "ExpirationModificationCodeId",
                table: "ModificationRules",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE r
                SET r.ExpirationModificationCodeId = c.Id
                FROM ModificationRules r
                INNER JOIN ExpirationModificationCodes c ON c.Code = UPPER(LTRIM(RTRIM(r.ExpirationOffsetCode)));

                UPDATE r
                SET r.ExpirationModificationCodeId = (SELECT TOP 1 Id FROM ExpirationModificationCodes WHERE Code = N'24H')
                FROM ModificationRules r
                WHERE r.ExpirationModificationCodeId IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "ExpirationModificationCodeId",
                table: "ModificationRules",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "ExpirationOffsetCode",
                table: "ModificationRules");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationRules_ExpirationModificationCodeId",
                table: "ModificationRules",
                column: "ExpirationModificationCodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModificationRules_ExpirationModificationCodes_ExpirationModificationCodeId",
                table: "ModificationRules",
                column: "ExpirationModificationCodeId",
                principalTable: "ExpirationModificationCodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AlterColumn<string>(
                name: "ExpirationOffsetCodeApplied",
                table: "UnitModifications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModificationRules_ExpirationModificationCodes_ExpirationModificationCodeId",
                table: "ModificationRules");

            migrationBuilder.DropIndex(
                name: "IX_ModificationRules_ExpirationModificationCodeId",
                table: "ModificationRules");

            migrationBuilder.AddColumn<string>(
                name: "ExpirationOffsetCode",
                table: "ModificationRules",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "24H");

            migrationBuilder.Sql("""
                UPDATE r
                SET r.ExpirationOffsetCode = LEFT(c.Code, 10)
                FROM ModificationRules r
                INNER JOIN ExpirationModificationCodes c ON c.Id = r.ExpirationModificationCodeId;
                """);

            migrationBuilder.DropColumn(
                name: "ExpirationModificationCodeId",
                table: "ModificationRules");

            migrationBuilder.DropTable(
                name: "ExpirationModificationCodes");

            migrationBuilder.AlterColumn<string>(
                name: "ExpirationOffsetCodeApplied",
                table: "UnitModifications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}
