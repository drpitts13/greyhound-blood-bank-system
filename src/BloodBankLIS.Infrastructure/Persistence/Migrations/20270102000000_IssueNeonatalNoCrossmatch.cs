using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-074: an issue to an infant without a crossmatch under the neonatal rules is marked (Cycle 674).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270102000000_IssueNeonatalNoCrossmatch")]
    public class IssueNeonatalNoCrossmatch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "NeonatalNoCrossmatch", table: "Issues", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NeonatalNoCrossmatch", table: "Issues");
        }
    }
}
