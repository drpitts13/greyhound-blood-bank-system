using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderingProvidersAndManualEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OrderingProviderId",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttendingProviderId",
                table: "Encounters",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderingProviders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderingProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderingProviderId",
                table: "Orders",
                column: "OrderingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_AttendingProviderId",
                table: "Encounters",
                column: "AttendingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderingProviders_IsActive",
                table: "OrderingProviders",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OrderingProviders_ProviderId",
                table: "OrderingProviders",
                column: "ProviderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_OrderingProviders_AttendingProviderId",
                table: "Encounters",
                column: "AttendingProviderId",
                principalTable: "OrderingProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_OrderingProviders_OrderingProviderId",
                table: "Orders",
                column: "OrderingProviderId",
                principalTable: "OrderingProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_OrderingProviders_AttendingProviderId",
                table: "Encounters");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_OrderingProviders_OrderingProviderId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "OrderingProviders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderingProviderId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_AttendingProviderId",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "OrderingProviderId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AttendingProviderId",
                table: "Encounters");
        }
    }
}
