using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-054: marks an issue entered after downtime from a paper record.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261106000000_IssueDowntimeBackEntry")]
    public class IssueDowntimeBackEntry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "DowntimeBackEntry", table: "Issues", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DowntimeBackEntry", table: "Issues");
        }
    }
}
