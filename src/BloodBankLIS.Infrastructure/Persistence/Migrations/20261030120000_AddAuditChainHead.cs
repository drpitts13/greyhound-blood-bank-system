using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the single-row audit chain head that writers lock instead of scanning
    /// AuditEvents for the unreferenced RecordHash. Seeded from the current tip.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261030120000_AddAuditChainHead")]
    public class AddAuditChainHead : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditChainHead",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TipHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditChainHead", x => x.Id);
                });

            // One last tip scan: the hashed row no other row points back to, else GENESIS.
            migrationBuilder.Sql(
                """
                INSERT INTO AuditChainHead (Id, TipHash)
                SELECT 1, COALESCE((
                    SELECT TOP 1 a.RecordHash
                    FROM AuditEvents a
                    WHERE a.RecordHash IS NOT NULL AND a.RecordHash <> ''
                      AND NOT EXISTS (
                          SELECT 1 FROM AuditEvents c WHERE c.PreviousHash = a.RecordHash)
                ), N'GENESIS');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AuditChainHead");
        }
    }
}
