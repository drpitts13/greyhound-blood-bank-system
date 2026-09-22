using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260920230000_UnitAttributeTypingSource")]
    public partial class UnitAttributeTypingSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TypingSource",
                table: "UnitBloodAttributes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE UnitBloodAttributes
                SET TypingSource = 2
                WHERE SourceResultId IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TypingSource",
                table: "UnitBloodAttributes");
        }
    }
}
