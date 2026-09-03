using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260903010000_ReactionWorkupChecklist")]
    public partial class ReactionWorkupChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClericalCheckCompleted",
                table: "ReactionInvestigations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ClericalCheckNotes",
                table: "ReactionInvestigations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisualInspectionCompleted",
                table: "ReactionInvestigations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VisualInspectionAcceptable",
                table: "ReactionInvestigations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RepeatPatientAboRh",
                table: "ReactionInvestigations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepeatUnitAboRh",
                table: "ReactionInvestigations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DatResult",
                table: "ReactionInvestigations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ElutionResult",
                table: "ReactionInvestigations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RemainderQuarantined",
                table: "ReactionInvestigations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ClericalCheckCompleted", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "ClericalCheckNotes", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "VisualInspectionCompleted", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "VisualInspectionAcceptable", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "RepeatPatientAboRh", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "RepeatUnitAboRh", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "DatResult", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "ElutionResult", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "RemainderQuarantined", table: "ReactionInvestigations");
        }
    }
}
