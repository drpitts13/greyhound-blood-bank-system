using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Coded specimen rejection reasons and the code stored on each rejected specimen.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103170000_SpecimenRejectionReasons")]
    public class SpecimenRejectionReasons : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecimenRejectionReasons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequiresComment = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_SpecimenRejectionReasons", x => x.Id);
                });
            migrationBuilder.CreateIndex(name: "IX_SpecimenRejectionReasons_Code", table: "SpecimenRejectionReasons", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_SpecimenRejectionReasons_IsActive_SortOrder", table: "SpecimenRejectionReasons", columns: new[] { "IsActive", "SortOrder" });
            migrationBuilder.AddColumn<string>(name: "RejectionReasonCode", table: "Specimens", type: "nvarchar(20)", maxLength: 20, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RejectionReasonCode", table: "Specimens");
            migrationBuilder.DropTable(name: "SpecimenRejectionReasons");
        }
    }
}
