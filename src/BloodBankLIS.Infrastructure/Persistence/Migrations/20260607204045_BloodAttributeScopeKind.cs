using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BloodAttributeScopeKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BloodAttributeScopeKind",
                table: "TestDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE TestDefinitions
                SET BloodAttributeScopeKind = 0
                WHERE ResultValueType = 5 AND BloodAttributeScopeKind IS NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BloodAttributeScopeKind",
                table: "TestDefinitions");
        }
    }
}
