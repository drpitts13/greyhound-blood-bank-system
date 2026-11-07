using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Massive transfusion protocol activations; each massive-transfusion issue links to one.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103090000_MtpActivations")]
    public class MtpActivations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MtpActivations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActivatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MtpActivations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MtpActivations_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_MtpActivations_PatientId_Status", table: "MtpActivations", columns: new[] { "PatientId", "Status" });

            migrationBuilder.AddColumn<long>(name: "MtpActivationId", table: "Issues", type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Issues_MtpActivationId", table: "Issues", column: "MtpActivationId");
            migrationBuilder.AddForeignKey(
                name: "FK_Issues_MtpActivations_MtpActivationId",
                table: "Issues",
                column: "MtpActivationId",
                principalTable: "MtpActivations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Issues_MtpActivations_MtpActivationId", table: "Issues");
            migrationBuilder.DropIndex(name: "IX_Issues_MtpActivationId", table: "Issues");
            migrationBuilder.DropColumn(name: "MtpActivationId", table: "Issues");
            migrationBuilder.DropTable(name: "MtpActivations");
        }
    }
}
