using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260919200000_SpecimenTypeExpiration")]
    public partial class SpecimenTypeExpiration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpirationCode",
                table: "SpecimenTypeDefinitions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "7D");

            migrationBuilder.AddColumn<int>(
                name: "ExpirationMode",
                table: "SpecimenTypeDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE SpecimenTypeDefinitions
                SET ExpirationCode = '3D'
                WHERE Code = 'SERUM' AND (ExpirationCode IS NULL OR ExpirationCode = '7D');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpirationCode",
                table: "SpecimenTypeDefinitions");

            migrationBuilder.DropColumn(
                name: "ExpirationMode",
                table: "SpecimenTypeDefinitions");
        }
    }
}
