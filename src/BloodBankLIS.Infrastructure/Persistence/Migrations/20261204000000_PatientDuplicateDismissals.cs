using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Possible-duplicate pairs a reviewer found to be different people, with the reason.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261204000000_PatientDuplicateDismissals")]
    public class PatientDuplicateDismissals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientDuplicateDismissals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstPatientId = table.Column<long>(type: "bigint", nullable: false),
                    SecondPatientId = table.Column<long>(type: "bigint", nullable: false),
                    ReviewedReasons = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DismissedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DismissedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_PatientDuplicateDismissals", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PatientDuplicateDismissals_FirstPatientId_SecondPatientId",
                table: "PatientDuplicateDismissals",
                columns: new[] { "FirstPatientId", "SecondPatientId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PatientDuplicateDismissals");
        }
    }
}
