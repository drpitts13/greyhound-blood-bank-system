using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>RhIG evaluations on the patient chart (OCD-069).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261205000000_RhigEvaluations")]
    public class RhigEvaluations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RhigEvaluations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    Context = table.Column<int>(type: "int", nullable: false),
                    EventUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InfantRh = table.Column<int>(type: "int", nullable: false),
                    FetalBleedScreen = table.Column<int>(type: "int", nullable: false),
                    FetalBleedPercent = table.Column<decimal>(type: "decimal(7,3)", nullable: true),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    VialsRecommended = table.Column<int>(type: "int", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_RhigEvaluations", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_RhigEvaluations_PatientId",
                table: "RhigEvaluations",
                column: "PatientId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RhigEvaluations");
        }
    }
}
