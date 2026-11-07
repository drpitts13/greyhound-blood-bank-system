using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Reason an issue was entered after its stated issue time (late entry, for example after downtime).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103140000_IssueLateEntry")]
    public class IssueLateEntry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "LateEntryReason", table: "Issues", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LateEntryReason", table: "Issues");
        }
    }
}
