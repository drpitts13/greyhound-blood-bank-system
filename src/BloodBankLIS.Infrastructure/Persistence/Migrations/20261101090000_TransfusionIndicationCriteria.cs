using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261101090000_TransfusionIndicationCriteria")]
    public class TransfusionIndicationCriteria : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransfusionIndicationCriteria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ComponentClass = table.Column<int>(type: "int", nullable: false),
                    LabTestCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Comparison = table.Column<int>(type: "int", nullable: true),
                    Threshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LookbackHours = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_TransfusionIndicationCriteria", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_TransfusionIndicationCriteria_Code", table: "TransfusionIndicationCriteria", column: "Code", unique: true);

            migrationBuilder.AddColumn<int>(name: "PbmStatus", table: "OrderLines", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "PbmIndicationCode", table: "OrderLines", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PbmJustification", table: "OrderLines", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PbmDetail", table: "OrderLines", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TransfusionIndicationCriteria");
            migrationBuilder.DropColumn(name: "PbmStatus", table: "OrderLines");
            migrationBuilder.DropColumn(name: "PbmIndicationCode", table: "OrderLines");
            migrationBuilder.DropColumn(name: "PbmJustification", table: "OrderLines");
            migrationBuilder.DropColumn(name: "PbmDetail", table: "OrderLines");
        }
    }
}
