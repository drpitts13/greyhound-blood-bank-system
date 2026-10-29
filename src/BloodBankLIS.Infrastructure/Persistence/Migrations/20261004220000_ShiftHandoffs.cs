using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261004220000_ShiftHandoffs")]
    public class ShiftHandoffs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftHandoffs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RetrospectiveDue = table.Column<int>(type: "int", nullable: false),
                    RetrospectiveOverdue = table.Column<int>(type: "int", nullable: false),
                    OutstandingIssued = table.Column<int>(type: "int", nullable: false),
                    InTransit = table.Column<int>(type: "int", nullable: false),
                    InTransitOverdue = table.Column<int>(type: "int", nullable: false),
                    PendingTests = table.Column<int>(type: "int", nullable: false),
                    PendingSpecimens = table.Column<int>(type: "int", nullable: false),
                    OpenReactions = table.Column<int>(type: "int", nullable: false),
                    FatalityFollowUpDue = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AcknowledgedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftHandoffs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandoffs_RecordedUtc",
                table: "ShiftHandoffs",
                column: "RecordedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandoffs_AcknowledgedUtc",
                table: "ShiftHandoffs",
                column: "AcknowledgedUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ShiftHandoffs");
        }
    }
}
