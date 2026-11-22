using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Links a special requirement to the reaction investigation that led to it.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261110000000_SpecialRequirementReactionLink")]
    public class SpecialRequirementReactionLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(name: "ReactionInvestigationId", table: "SpecialTransfusionRequirements", type: "bigint", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReactionInvestigationId", table: "SpecialTransfusionRequirements");
        }
    }
}
