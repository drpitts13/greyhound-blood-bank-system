using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 115: the bedside consent confirmation on a transfusion.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270131000000_TransfusionConsent")]
    public class TransfusionConsent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "ConsentConfirmed", table: "TransfusionEvents", type: "bit", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ConsentConfirmed", table: "TransfusionEvents");
        }
    }
}
