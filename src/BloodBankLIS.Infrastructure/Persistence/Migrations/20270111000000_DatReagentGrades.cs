using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A DAT workup records the polyspecific, anti-IgG, anti-C3 and control reactions with their grades (end-user
    /// testing pass 9; the DAT was only Negative or Positive). 0 is not tested.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270111000000_DatReagentGrades")]
    public class DatReagentGrades : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "PolyspecificGrade", table: "DatElutionWorkups", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "IgGGrade", table: "DatElutionWorkups", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "C3Grade", table: "DatElutionWorkups", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "ControlGrade", table: "DatElutionWorkups", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PolyspecificGrade", table: "DatElutionWorkups");
            migrationBuilder.DropColumn(name: "IgGGrade", table: "DatElutionWorkups");
            migrationBuilder.DropColumn(name: "C3Grade", table: "DatElutionWorkups");
            migrationBuilder.DropColumn(name: "ControlGrade", table: "DatElutionWorkups");
        }
    }
}
