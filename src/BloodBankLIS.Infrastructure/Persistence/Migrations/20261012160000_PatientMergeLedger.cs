using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261012160000_PatientMergeLedger")]
    public class PatientMergeLedger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientMerges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurvivorPatientId = table.Column<long>(type: "bigint", nullable: false),
                    DuplicatePatientId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MergedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MergedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnmergeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UnmergedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnmergedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SurvivorDeceasedBefore = table.Column<bool>(type: "bit", nullable: false),
                    SurvivorDeceasedUtcBefore = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SurvivorRecentPregnancyUtcBefore = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SurvivorDeceasedAfter = table.Column<bool>(type: "bit", nullable: false),
                    SurvivorDeceasedUtcAfter = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SurvivorRecentPregnancyUtcAfter = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriorMrnIdentifierId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMerges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientMergeMoves",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientMergeId = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMergeMoves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientMergeMoves_PatientMerges_PatientMergeId",
                        column: x => x.PatientMergeId,
                        principalTable: "PatientMerges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientMerges_DuplicatePatientId",
                table: "PatientMerges",
                column: "DuplicatePatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMerges_SurvivorPatientId",
                table: "PatientMerges",
                column: "SurvivorPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMerges_DuplicatePatientId_Status",
                table: "PatientMerges",
                columns: new[] { "DuplicatePatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientMergeMoves_PatientMergeId",
                table: "PatientMergeMoves",
                column: "PatientMergeId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMergeMoves_EntityType_EntityId",
                table: "PatientMergeMoves",
                columns: new[] { "EntityType", "EntityId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PatientMergeMoves");
            migrationBuilder.DropTable(name: "PatientMerges");
        }
    }
}
