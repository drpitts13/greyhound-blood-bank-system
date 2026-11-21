using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Named specimen label designs for tubes and aliquots, each its own size (Cycle 657).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261223000000_SpecimenLabelDesigns")]
    public class SpecimenLabelDesigns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecimenLabelDesigns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    WidthDots = table.Column<int>(type: "int", nullable: false),
                    HeightDots = table.Column<int>(type: "int", nullable: false),
                    DocumentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecimenLabelDesigns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecimenLabelDesigns_Code",
                table: "SpecimenLabelDesigns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecimenLabelDesigns_Purpose_IsActive",
                table: "SpecimenLabelDesigns",
                columns: new[] { "Purpose", "IsActive" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SpecimenLabelDesigns");
        }
    }
}
