using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260903040000_ReceiveVisualInspection")]
    public partial class ReceiveVisualInspection : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReceiveVisualAcceptable",
                table: "BloodProducts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiveVisualNotes",
                table: "BloodProducts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReceiveVisualAcceptable", table: "BloodProducts");
            migrationBuilder.DropColumn(name: "ReceiveVisualNotes", table: "BloodProducts");
        }
    }
}
