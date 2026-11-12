using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts units a ward has asked to be sent (end-user testing pass 21).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270115000000_ShiftHandoffWardRequests")]
    public class ShiftHandoffWardRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "WardRequestsWaiting", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "WardRequestsWaiting", table: "ShiftHandoffs");
        }
    }
}
