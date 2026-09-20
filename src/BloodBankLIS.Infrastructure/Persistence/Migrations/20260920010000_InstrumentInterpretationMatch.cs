using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260920010000_InstrumentInterpretationMatch")]
    public partial class InstrumentInterpretationMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InterpretationMatch",
                table: "InstrumentInterfaces",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InterpretationAnalyteCodes",
                table: "InstrumentInterfaces",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterpretationMatch",
                table: "InstrumentInterfaces");

            migrationBuilder.DropColumn(
                name: "InterpretationAnalyteCodes",
                table: "InstrumentInterfaces");
        }
    }
}
