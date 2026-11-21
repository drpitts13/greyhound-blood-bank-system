using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A cryoprecipitate product may allow mixed-ABO pools (mixed-ABO pooling phase 3, OCD-094).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270223000000_AllowMixedAboPool")]
    public class AllowMixedAboPool : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "AllowMixedAboPool", table: "ProductTypes", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AllowMixedAboPool", table: "ProductTypes");
        }
    }
}
