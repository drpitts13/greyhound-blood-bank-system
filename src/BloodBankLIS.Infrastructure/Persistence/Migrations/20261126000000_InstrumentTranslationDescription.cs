using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>An instrument translation row can say what its value means: each error code's meaning in the error code table.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261126000000_InstrumentTranslationDescription")]
    public class InstrumentTranslationDescription : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Description", table: "InstrumentValueTranslations", type: "nvarchar(200)", maxLength: 200, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Description", table: "InstrumentValueTranslations");
        }
    }
}
