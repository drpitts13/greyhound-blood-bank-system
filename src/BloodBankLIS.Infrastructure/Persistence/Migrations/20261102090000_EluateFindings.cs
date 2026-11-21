using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-042: structured eluate findings and second-person verification of DAT/elution workups.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261102090000_EluateFindings")]
    public class EluateFindings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "VerifiedBy", table: "DatElutionWorkups", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "VerifiedUtc", table: "DatElutionWorkups", type: "datetime2", nullable: true);

            migrationBuilder.CreateTable(
                name: "DatElutionEluateFindings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkupId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    BloodAttributeDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    Specificity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PostedAntibodyHistoryId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatElutionEluateFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatElutionEluateFindings_DatElutionWorkups_WorkupId",
                        column: x => x.WorkupId,
                        principalTable: "DatElutionWorkups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_DatElutionEluateFindings_WorkupId", table: "DatElutionEluateFindings", column: "WorkupId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DatElutionEluateFindings");
            migrationBuilder.DropColumn(name: "VerifiedBy", table: "DatElutionWorkups");
            migrationBuilder.DropColumn(name: "VerifiedUtc", table: "DatElutionWorkups");
        }
    }
}
