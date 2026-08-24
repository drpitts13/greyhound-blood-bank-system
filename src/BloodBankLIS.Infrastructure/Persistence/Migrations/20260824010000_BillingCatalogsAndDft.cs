using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260824010000_BillingCatalogsAndDft")]
    public partial class BillingCatalogsAndDft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingCode",
                table: "BillingEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceKind",
                table: "BillingEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "SourceId",
                table: "BillingEvents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Hl7MessageId",
                table: "BillingEvents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BillingEvents",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<long>(
                name: "ChargeCodeId",
                table: "BillingEvents",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.Sql(
                """
                UPDATE e
                SET e.BillingCode = c.Code
                FROM BillingEvents e
                INNER JOIN ChargeCodes c ON c.Id = e.ChargeCodeId
                WHERE e.BillingCode IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE BillingEvents
                SET BillingCode = 'UNKNOWN'
                WHERE BillingCode IS NULL OR BillingCode = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE BillingEvents
                SET SourceId = ChargeCodeId
                WHERE SourceId IS NULL AND ChargeCodeId IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE BillingEvents
                SET SourceId = 0
                WHERE SourceId IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "BillingCode",
                table: "BillingEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "SourceId",
                table: "BillingEvents",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingEvents_SourceKind_SourceId",
                table: "BillingEvents",
                columns: new[] { "SourceKind", "SourceId" });

            migrationBuilder.CreateTable(
                name: "TestServiceBillings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    TestCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestServiceBillings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductBillings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    IsbtProductCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBillings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestServiceBillings_Trigger_TestCode",
                table: "TestServiceBillings",
                columns: new[] { "Trigger", "TestCode" });

            migrationBuilder.CreateIndex(
                name: "IX_TestServiceBillings_Trigger_TestCode_BillingCode",
                table: "TestServiceBillings",
                columns: new[] { "Trigger", "TestCode", "BillingCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBillings_Trigger_IsbtProductCode",
                table: "ProductBillings",
                columns: new[] { "Trigger", "IsbtProductCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBillings_Trigger_IsbtProductCode_BillingCode",
                table: "ProductBillings",
                columns: new[] { "Trigger", "IsbtProductCode", "BillingCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TestServiceBillings");
            migrationBuilder.DropTable(name: "ProductBillings");

            migrationBuilder.DropIndex(
                name: "IX_BillingEvents_SourceKind_SourceId",
                table: "BillingEvents");

            migrationBuilder.DropColumn(name: "BillingCode", table: "BillingEvents");
            migrationBuilder.DropColumn(name: "SourceKind", table: "BillingEvents");
            migrationBuilder.DropColumn(name: "SourceId", table: "BillingEvents");
            migrationBuilder.DropColumn(name: "Hl7MessageId", table: "BillingEvents");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BillingEvents",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ChargeCodeId",
                table: "BillingEvents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
