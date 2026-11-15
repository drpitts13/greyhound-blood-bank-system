using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-091: when a hand-entered result was tested, if earlier than it was entered.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270126000000_TestResultTestedTime")]
    public class TestResultTestedTime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "TestedUtc", table: "TestResults", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TestedUtc", table: "TestResults");
        }
    }
}
