using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Records how an override's authorizer matched the provider and user directory.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103190000_OverrideAuthorizerMatch")]
    public class OverrideAuthorizerMatch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "AuthorizerMatch", table: "Overrides", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AuthorizerReference", table: "Overrides", type: "nvarchar(100)", maxLength: 100, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AuthorizerReference", table: "Overrides");
            migrationBuilder.DropColumn(name: "AuthorizerMatch", table: "Overrides");
        }
    }
}
