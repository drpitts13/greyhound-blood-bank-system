using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Transfusion reaction workup: the Test Group for each workup specimen (singleton settings), and the pre- and
    /// post-transfusion specimens and their orders on each investigation.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261114000000_ReactionWorkupSpecimens")]
    public class ReactionWorkupSpecimens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReactionWorkupSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreTransfusionTestGroupCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PostTransfusionTestGroupCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    IsPendingApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionWorkupSettings", x => x.Id);
                });
            migrationBuilder.AddColumn<long>(name: "PreTransfusionSpecimenId", table: "ReactionInvestigations", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<long>(name: "PreTransfusionOrderId", table: "ReactionInvestigations", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<long>(name: "PostTransfusionSpecimenId", table: "ReactionInvestigations", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<long>(name: "PostTransfusionOrderId", table: "ReactionInvestigations", type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_ReactionInvestigations_PreTransfusionOrderId", table: "ReactionInvestigations", column: "PreTransfusionOrderId");
            migrationBuilder.CreateIndex(name: "IX_ReactionInvestigations_PostTransfusionOrderId", table: "ReactionInvestigations", column: "PostTransfusionOrderId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_ReactionInvestigations_PreTransfusionOrderId", table: "ReactionInvestigations");
            migrationBuilder.DropIndex(name: "IX_ReactionInvestigations_PostTransfusionOrderId", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "PreTransfusionSpecimenId", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "PreTransfusionOrderId", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "PostTransfusionSpecimenId", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "PostTransfusionOrderId", table: "ReactionInvestigations");
            migrationBuilder.DropTable(name: "ReactionWorkupSettings");
        }
    }
}
