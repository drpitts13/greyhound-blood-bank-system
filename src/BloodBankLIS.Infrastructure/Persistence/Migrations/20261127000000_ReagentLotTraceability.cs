using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reagent lot traceability: each reagent lists the tests it is used for, and a manual result records the lots of
    /// those reagents open when it was entered, so a lot that fails QC can be traced to its results.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261127000000_ReagentLotTraceability")]
    public class ReagentLotTraceability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "TestCodes", table: "Reagents", type: "nvarchar(max)", nullable: true);
            migrationBuilder.CreateTable(
                name: "TestResultReagentLots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TestResultId = table.Column<long>(type: "bigint", nullable: false),
                    ReagentLotId = table.Column<long>(type: "bigint", nullable: false),
                    ReagentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RecordedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_TestResultReagentLots", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_TestResultReagentLots_ReagentLotId", table: "TestResultReagentLots", column: "ReagentLotId");
            migrationBuilder.CreateIndex(name: "IX_TestResultReagentLots_TestResultId", table: "TestResultReagentLots", column: "TestResultId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TestResultReagentLots");
            migrationBuilder.DropColumn(name: "TestCodes", table: "Reagents");
        }
    }
}
