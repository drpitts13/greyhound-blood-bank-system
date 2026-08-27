using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260825200000_ProductRetype")]
    public partial class ProductRetype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresRetype",
                table: "ProductTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProductRetypeResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    TestDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    TestCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    InterpretedAbo = table.Column<int>(type: "int", nullable: false),
                    InterpretedRh = table.Column<int>(type: "int", nullable: true),
                    MatchesLabel = table.Column<bool>(type: "bit", nullable: false),
                    DiscrepancyDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EnteredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnteredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VerifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRetypeResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRetypeResults_BloodProducts_BloodProductId",
                        column: x => x.BloodProductId,
                        principalTable: "BloodProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductRetypeResults_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductRetypeResults_BloodProductId",
                table: "ProductRetypeResults",
                column: "BloodProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRetypeResults_BloodProductId_EnteredUtc",
                table: "ProductRetypeResults",
                columns: new[] { "BloodProductId", "EnteredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductRetypeResults_TestDefinitionId",
                table: "ProductRetypeResults",
                column: "TestDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductRetypeResults");

            migrationBuilder.DropColumn(
                name: "RequiresRetype",
                table: "ProductTypes");
        }
    }
}
