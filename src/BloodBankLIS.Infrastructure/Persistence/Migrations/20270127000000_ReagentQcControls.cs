using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 93: a reagent's daily QC controls, and the control each QC reading is for.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270127000000_ReagentQcControls")]
    public class ReagentQcControls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "QcControls", table: "Reagents", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Control", table: "ReagentDailyQcs", type: "nvarchar(100)", maxLength: 100, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "QcControls", table: "Reagents");
            migrationBuilder.DropColumn(name: "Control", table: "ReagentDailyQcs");
        }
    }
}
