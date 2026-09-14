using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260905211400_AuditHashChain")]
    public partial class AuditHashChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousHash",
                table: "AuditEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordHash",
                table: "AuditEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_PreviousHash",
                table: "AuditEvents",
                column: "PreviousHash",
                unique: true,
                filter: "[PreviousHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_RecordHash",
                table: "AuditEvents",
                column: "RecordHash",
                unique: true,
                filter: "[RecordHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_PreviousHash",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_RecordHash",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "PreviousHash",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "RecordHash",
                table: "AuditEvents");
        }
    }
}
