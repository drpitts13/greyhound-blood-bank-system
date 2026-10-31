using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261031160000_Alerts")]
    public class Alerts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RaisedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AcknowledgedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgeNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_Alerts", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Source_ResolvedUtc",
                table: "Alerts",
                columns: new[] { "Source", "ResolvedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Key",
                table: "Alerts",
                column: "Key");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Alerts");
        }
    }
}
