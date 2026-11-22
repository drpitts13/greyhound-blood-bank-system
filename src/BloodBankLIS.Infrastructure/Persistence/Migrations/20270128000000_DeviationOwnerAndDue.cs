using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 98: who a deviation is assigned to and when it is due.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270128000000_DeviationOwnerAndDue")]
    public class DeviationOwnerAndDue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "AssignedTo", table: "Deviations", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateOnly>(name: "DueOn", table: "Deviations", type: "date", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AssignedTo", table: "Deviations");
            migrationBuilder.DropColumn(name: "DueOn", table: "Deviations");
        }
    }
}
