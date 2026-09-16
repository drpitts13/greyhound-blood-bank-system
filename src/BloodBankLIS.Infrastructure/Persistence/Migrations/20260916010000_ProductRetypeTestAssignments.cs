using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260916010000_ProductRetypeTestAssignments")]
    public partial class ProductRetypeTestAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RhPositiveRetypeTestId",
                table: "ProductTypes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RhNegativeRetypeTestId",
                table: "ProductTypes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypes_RhPositiveRetypeTestId",
                table: "ProductTypes",
                column: "RhPositiveRetypeTestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypes_RhNegativeRetypeTestId",
                table: "ProductTypes",
                column: "RhNegativeRetypeTestId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductTypes_TestDefinitions_RhPositiveRetypeTestId",
                table: "ProductTypes",
                column: "RhPositiveRetypeTestId",
                principalTable: "TestDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductTypes_TestDefinitions_RhNegativeRetypeTestId",
                table: "ProductTypes",
                column: "RhNegativeRetypeTestId",
                principalTable: "TestDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductTypes_TestDefinitions_RhPositiveRetypeTestId",
                table: "ProductTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductTypes_TestDefinitions_RhNegativeRetypeTestId",
                table: "ProductTypes");

            migrationBuilder.DropIndex(
                name: "IX_ProductTypes_RhPositiveRetypeTestId",
                table: "ProductTypes");

            migrationBuilder.DropIndex(
                name: "IX_ProductTypes_RhNegativeRetypeTestId",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "RhPositiveRetypeTestId",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "RhNegativeRetypeTestId",
                table: "ProductTypes");
        }
    }
}
