using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A corrected antigram supersedes a panel lot's cells instead of deleting them (Cycle 706).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270107000000_AntibodyPanelCellSuperseded")]
    public class AntibodyPanelCellSuperseded : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "SupersededUtc", table: "AntibodyPanelCells", type: "datetime2", nullable: true);
            migrationBuilder.DropIndex(name: "IX_AntibodyPanelCells_LotId_CellNumber", table: "AntibodyPanelCells");
            migrationBuilder.CreateIndex(
                name: "IX_AntibodyPanelCells_LotId_CellNumber",
                table: "AntibodyPanelCells",
                columns: new[] { "LotId", "CellNumber" },
                unique: true,
                filter: "[SupersededUtc] IS NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_AntibodyPanelCells_LotId_CellNumber", table: "AntibodyPanelCells");
            migrationBuilder.CreateIndex(
                name: "IX_AntibodyPanelCells_LotId_CellNumber",
                table: "AntibodyPanelCells",
                columns: new[] { "LotId", "CellNumber" },
                unique: true);
            migrationBuilder.DropColumn(name: "SupersededUtc", table: "AntibodyPanelCells");
        }
    }
}
