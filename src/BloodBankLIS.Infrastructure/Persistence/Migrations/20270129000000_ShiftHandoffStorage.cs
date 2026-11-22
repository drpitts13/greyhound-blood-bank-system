using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 103: storage temperature and overdue deviations on the shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270129000000_ShiftHandoffStorage")]
    public class ShiftHandoffStorage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "TemperatureExcursionsOpen", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "TemperatureExcursionsToClose", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "TemperatureReadingsDue", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "DeviationsOverdue", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TemperatureExcursionsOpen", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "TemperatureExcursionsToClose", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "TemperatureReadingsDue", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "DeviationsOverdue", table: "ShiftHandoffs");
        }
    }
}
