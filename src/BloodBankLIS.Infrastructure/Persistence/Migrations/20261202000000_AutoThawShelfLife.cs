using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Each auto-thaw product code carries its thawed shelf life; existing codes keep 24 hours.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261202000000_AutoThawShelfLife")]
    public class AutoThawShelfLife : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ThawedShelfLifeHours", table: "AutoThawProducts", type: "int", nullable: false, defaultValue: 24);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ThawedShelfLifeHours", table: "AutoThawProducts");
        }
    }
}
