using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A blood type whose source result was invalidated: never current again, not evidence.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103160000_BloodTypeInvalidated")]
    public class BloodTypeInvalidated : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "InvalidatedUtc", table: "PatientBloodTypeHistory", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "InvalidatedUtc", table: "PatientBloodTypeHistory");
        }
    }
}
