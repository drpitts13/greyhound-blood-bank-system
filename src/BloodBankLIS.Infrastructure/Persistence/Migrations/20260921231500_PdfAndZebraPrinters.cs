using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260921231500_PdfAndZebraPrinters")]
    public partial class PdfAndZebraPrinters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfPrinters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WindowsPrinterName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfPrinters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZebraPrinters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConnectionType = table.Column<int>(type: "int", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Port = table.Column<int>(type: "int", nullable: false),
                    WindowsPrinterName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ComPort = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    BaudRate = table.Column<int>(type: "int", nullable: false),
                    DataBits = table.Column<int>(type: "int", nullable: false),
                    Parity = table.Column<int>(type: "int", nullable: false),
                    StopBits = table.Column<int>(type: "int", nullable: false),
                    Dpi = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZebraPrinters", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatus",
                table: "PrintJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryError",
                table: "PrintJobs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredUtc",
                table: "PrintJobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RenderedPdf",
                table: "PrintJobs",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PdfPrinterId",
                table: "PrintJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ZebraPrinterId",
                table: "PrintJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfPrinters_IsActive",
                table: "PdfPrinters",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ZebraPrinters_IsActive",
                table: "ZebraPrinters",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_PdfPrinterId",
                table: "PrintJobs",
                column: "PdfPrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_ZebraPrinterId",
                table: "PrintJobs",
                column: "ZebraPrinterId");

            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_PdfPrinters_PdfPrinterId",
                table: "PrintJobs",
                column: "PdfPrinterId",
                principalTable: "PdfPrinters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_ZebraPrinters_ZebraPrinterId",
                table: "PrintJobs",
                column: "ZebraPrinterId",
                principalTable: "ZebraPrinters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrintJobs_PdfPrinters_PdfPrinterId",
                table: "PrintJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_PrintJobs_ZebraPrinters_ZebraPrinterId",
                table: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_PdfPrinterId",
                table: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_ZebraPrinterId",
                table: "PrintJobs");

            migrationBuilder.DropColumn(name: "DeliveryStatus", table: "PrintJobs");
            migrationBuilder.DropColumn(name: "DeliveryError", table: "PrintJobs");
            migrationBuilder.DropColumn(name: "DeliveredUtc", table: "PrintJobs");
            migrationBuilder.DropColumn(name: "RenderedPdf", table: "PrintJobs");
            migrationBuilder.DropColumn(name: "PdfPrinterId", table: "PrintJobs");
            migrationBuilder.DropColumn(name: "ZebraPrinterId", table: "PrintJobs");

            migrationBuilder.DropTable(name: "PdfPrinters");
            migrationBuilder.DropTable(name: "ZebraPrinters");
        }
    }
}
