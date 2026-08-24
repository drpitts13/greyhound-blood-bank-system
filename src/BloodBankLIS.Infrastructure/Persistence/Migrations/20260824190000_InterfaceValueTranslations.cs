using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260824190000_InterfaceValueTranslations")]
    public partial class InterfaceValueTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterfaceValueTranslations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataItemKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InternalValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterfaceValueTranslations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceValueTranslations_DataItemKey",
                table: "InterfaceValueTranslations",
                column: "DataItemKey");

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceValueTranslations_DataItemKey_InternalValue_ExternalValue_Direction",
                table: "InterfaceValueTranslations",
                columns: new[] { "DataItemKey", "InternalValue", "ExternalValue", "Direction" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterfaceValueTranslations");
        }
    }
}
