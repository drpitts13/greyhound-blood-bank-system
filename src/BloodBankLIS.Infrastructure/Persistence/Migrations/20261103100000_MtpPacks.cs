using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Facility MTP pack definitions (none shipped, OCD-012) and the optional pack on an activation.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103100000_MtpPacks")]
    public class MtpPacks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MtpPackDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RedCellUnits = table.Column<int>(type: "int", nullable: false),
                    PlasmaUnits = table.Column<int>(type: "int", nullable: false),
                    PlateletUnits = table.Column<int>(type: "int", nullable: false),
                    CryoUnits = table.Column<int>(type: "int", nullable: false),
                    WholeBloodUnits = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MtpPackDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_MtpPackDefinitions_Code", table: "MtpPackDefinitions", column: "Code", unique: true);

            migrationBuilder.AddColumn<long>(name: "PackDefinitionId", table: "MtpActivations", type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_MtpActivations_PackDefinitionId", table: "MtpActivations", column: "PackDefinitionId");
            migrationBuilder.AddForeignKey(
                name: "FK_MtpActivations_MtpPackDefinitions_PackDefinitionId",
                table: "MtpActivations",
                column: "PackDefinitionId",
                principalTable: "MtpPackDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_MtpActivations_MtpPackDefinitions_PackDefinitionId", table: "MtpActivations");
            migrationBuilder.DropIndex(name: "IX_MtpActivations_PackDefinitionId", table: "MtpActivations");
            migrationBuilder.DropColumn(name: "PackDefinitionId", table: "MtpActivations");
            migrationBuilder.DropTable(name: "MtpPackDefinitions");
        }
    }
}
