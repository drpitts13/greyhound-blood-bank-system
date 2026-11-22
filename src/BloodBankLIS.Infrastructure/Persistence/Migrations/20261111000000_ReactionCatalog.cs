using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-057: admin lists of reactions and treatments, and the conclusion recorded on an investigation.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261111000000_ReactionCatalog")]
    public class ReactionCatalog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReactionCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsNoneOption = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionCatalogEntries", x => x.Id);
                });
            migrationBuilder.CreateIndex(name: "IX_ReactionCatalogEntries_Kind_Code", table: "ReactionCatalogEntries", columns: new[] { "Kind", "Code" }, unique: true);
            migrationBuilder.AddColumn<string>(name: "ConcludedReactionCode", table: "ReactionInvestigations", type: "nvarchar(30)", maxLength: 30, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ConcludedReactionText", table: "ReactionInvestigations", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "TreatmentCodes", table: "ReactionInvestigations", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(name: "TreatmentTexts", table: "ReactionInvestigations", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ConcludedReactionCode", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "ConcludedReactionText", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "TreatmentCodes", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "TreatmentTexts", table: "ReactionInvestigations");
            migrationBuilder.DropTable(name: "ReactionCatalogEntries");
        }
    }
}
