using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260824020000_BillingCatalogChargeCodeFk")]
    public partial class BillingCatalogChargeCodeFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ChargeCodeId",
                table: "TestServiceBillings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ChargeCodeId",
                table: "ProductBillings",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO ChargeCodes (Code, Description, DefaultAmount, IsActive, CreatedUtc, CreatedBy)
                SELECT DISTINCT t.BillingCode,
                       COALESCE(NULLIF(LTRIM(RTRIM(t.Description)), ''), t.BillingCode),
                       COALESCE(t.Price, 0),
                       1,
                       SYSUTCDATETIME(),
                       'system'
                FROM TestServiceBillings t
                WHERE t.BillingCode IS NOT NULL AND t.BillingCode <> ''
                  AND NOT EXISTS (SELECT 1 FROM ChargeCodes c WHERE c.Code = t.BillingCode);

                INSERT INTO ChargeCodes (Code, Description, DefaultAmount, IsActive, CreatedUtc, CreatedBy)
                SELECT DISTINCT p.BillingCode,
                       COALESCE(NULLIF(LTRIM(RTRIM(p.Description)), ''), p.BillingCode),
                       COALESCE(p.Price, 0),
                       1,
                       SYSUTCDATETIME(),
                       'system'
                FROM ProductBillings p
                WHERE p.BillingCode IS NOT NULL AND p.BillingCode <> ''
                  AND NOT EXISTS (SELECT 1 FROM ChargeCodes c WHERE c.Code = p.BillingCode);

                UPDATE t
                SET t.ChargeCodeId = c.Id
                FROM TestServiceBillings t
                INNER JOIN ChargeCodes c ON c.Code = t.BillingCode;

                UPDATE p
                SET p.ChargeCodeId = c.Id
                FROM ProductBillings p
                INNER JOIN ChargeCodes c ON c.Code = p.BillingCode;

                DELETE FROM TestServiceBillings WHERE ChargeCodeId IS NULL;
                DELETE FROM ProductBillings WHERE ChargeCodeId IS NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_TestServiceBillings_Trigger_TestCode_BillingCode",
                table: "TestServiceBillings");

            migrationBuilder.DropIndex(
                name: "IX_ProductBillings_Trigger_IsbtProductCode_BillingCode",
                table: "ProductBillings");

            migrationBuilder.DropColumn(name: "BillingCode", table: "TestServiceBillings");
            migrationBuilder.DropColumn(name: "Price", table: "TestServiceBillings");
            migrationBuilder.DropColumn(name: "BillingCode", table: "ProductBillings");
            migrationBuilder.DropColumn(name: "Price", table: "ProductBillings");

            migrationBuilder.AlterColumn<long>(
                name: "ChargeCodeId",
                table: "TestServiceBillings",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ChargeCodeId",
                table: "ProductBillings",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestServiceBillings_ChargeCodeId",
                table: "TestServiceBillings",
                column: "ChargeCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_TestServiceBillings_Trigger_TestCode_ChargeCodeId",
                table: "TestServiceBillings",
                columns: new[] { "Trigger", "TestCode", "ChargeCodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBillings_ChargeCodeId",
                table: "ProductBillings",
                column: "ChargeCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBillings_Trigger_IsbtProductCode_ChargeCodeId",
                table: "ProductBillings",
                columns: new[] { "Trigger", "IsbtProductCode", "ChargeCodeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TestServiceBillings_ChargeCodes_ChargeCodeId",
                table: "TestServiceBillings",
                column: "ChargeCodeId",
                principalTable: "ChargeCodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBillings_ChargeCodes_ChargeCodeId",
                table: "ProductBillings",
                column: "ChargeCodeId",
                principalTable: "ChargeCodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestServiceBillings_ChargeCodes_ChargeCodeId",
                table: "TestServiceBillings");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBillings_ChargeCodes_ChargeCodeId",
                table: "ProductBillings");

            migrationBuilder.DropIndex(
                name: "IX_TestServiceBillings_ChargeCodeId",
                table: "TestServiceBillings");

            migrationBuilder.DropIndex(
                name: "IX_TestServiceBillings_Trigger_TestCode_ChargeCodeId",
                table: "TestServiceBillings");

            migrationBuilder.DropIndex(
                name: "IX_ProductBillings_ChargeCodeId",
                table: "ProductBillings");

            migrationBuilder.DropIndex(
                name: "IX_ProductBillings_Trigger_IsbtProductCode_ChargeCodeId",
                table: "ProductBillings");

            migrationBuilder.AddColumn<string>(
                name: "BillingCode",
                table: "TestServiceBillings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "TestServiceBillings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCode",
                table: "ProductBillings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "ProductBillings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE t
                SET t.BillingCode = c.Code, t.Price = c.DefaultAmount
                FROM TestServiceBillings t
                INNER JOIN ChargeCodes c ON c.Id = t.ChargeCodeId;

                UPDATE p
                SET p.BillingCode = c.Code, p.Price = c.DefaultAmount
                FROM ProductBillings p
                INNER JOIN ChargeCodes c ON c.Id = p.ChargeCodeId;

                UPDATE TestServiceBillings SET BillingCode = 'UNKNOWN' WHERE BillingCode IS NULL OR BillingCode = '';
                UPDATE ProductBillings SET BillingCode = 'UNKNOWN' WHERE BillingCode IS NULL OR BillingCode = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "BillingCode",
                table: "TestServiceBillings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BillingCode",
                table: "ProductBillings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.DropColumn(name: "ChargeCodeId", table: "TestServiceBillings");
            migrationBuilder.DropColumn(name: "ChargeCodeId", table: "ProductBillings");

            migrationBuilder.CreateIndex(
                name: "IX_TestServiceBillings_Trigger_TestCode_BillingCode",
                table: "TestServiceBillings",
                columns: new[] { "Trigger", "TestCode", "BillingCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBillings_Trigger_IsbtProductCode_BillingCode",
                table: "ProductBillings",
                columns: new[] { "Trigger", "IsbtProductCode", "BillingCode" },
                unique: true);
        }
    }
}
