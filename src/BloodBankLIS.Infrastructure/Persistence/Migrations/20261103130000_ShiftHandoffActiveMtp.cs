using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Active massive transfusion protocols counted on each shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103130000_ShiftHandoffActiveMtp")]
    public class ShiftHandoffActiveMtp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ActiveMtp", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ActiveMtp", table: "ShiftHandoffs");
        }
    }
}
