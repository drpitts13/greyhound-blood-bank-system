using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260922010000_LabelLayouts")]
    public partial class LabelLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabelLayouts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    DocumentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelLayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PtagIdentifiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BloodProductId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    PrintJobId = table.Column<long>(type: "bigint", nullable: true),
                    IssueId = table.Column<long>(type: "bigint", nullable: true),
                    ConsumedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PtagIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PtagIdentifiers_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<string>(
                name: "PtagCode",
                table: "Issues",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabelLayouts_Kind",
                table: "LabelLayouts",
                column: "Kind",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PtagIdentifiers_Code",
                table: "PtagIdentifiers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PtagIdentifiers_BloodProductId_PatientId_IssueId",
                table: "PtagIdentifiers",
                columns: new[] { "BloodProductId", "PatientId", "IssueId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PtagIdentifiers");
            migrationBuilder.DropTable(name: "LabelLayouts");
            migrationBuilder.DropColumn(name: "PtagCode", table: "Issues");
        }
    }
}
