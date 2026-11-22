using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A pool records the ABO/Rh of each source unit (mixed-ABO pooling, phase 1).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270222000000_PoolComponents")]
    public class PoolComponents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "PoolComponentsJson", table: "BloodProducts", type: "nvarchar(max)", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PoolComponentsJson", table: "BloodProducts");
        }
    }
}
