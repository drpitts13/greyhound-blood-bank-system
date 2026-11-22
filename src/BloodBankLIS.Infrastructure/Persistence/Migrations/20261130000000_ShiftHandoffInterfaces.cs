using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts open HL7 errors and unmapped interface values.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261130000000_ShiftHandoffInterfaces")]
    public class ShiftHandoffInterfaces : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "Hl7ErrorsOpen", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "UnmappedInterfaceValues", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UnmappedInterfaceValues", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "Hl7ErrorsOpen", table: "ShiftHandoffs");
        }
    }
}
