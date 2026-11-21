using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Interface translations can be limited to one interface (EndpointId), and values an interface needed that had no
    /// translation are listed in InterfaceUnmappedValues for the interface team.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261122000000_InterfaceTranslationScopeAndUnmapped")]
    public class InterfaceTranslationScopeAndUnmapped : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(name: "EndpointId", table: "InterfaceValueTranslations", type: "bigint", nullable: true);
            migrationBuilder.DropIndex(
                name: "IX_InterfaceValueTranslations_DataItemKey_InternalValue_ExternalValue_Direction",
                table: "InterfaceValueTranslations");
            migrationBuilder.CreateIndex(
                name: "IX_InterfaceValueTranslations_EndpointId_DataItemKey_InternalValue_ExternalValue_Direction",
                table: "InterfaceValueTranslations",
                columns: new[] { "EndpointId", "DataItemKey", "InternalValue", "ExternalValue", "Direction" },
                unique: true,
                filter: null);

            migrationBuilder.CreateTable(
                name: "InterfaceUnmappedValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    EndpointId = table.Column<long>(type: "bigint", nullable: true),
                    DataItemKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    LastMessageControlId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResolvedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResolvedNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_InterfaceUnmappedValues", x => x.Id));
            migrationBuilder.CreateIndex(
                name: "IX_InterfaceUnmappedValues_EndpointId_DataItemKey_Direction_Value",
                table: "InterfaceUnmappedValues",
                columns: new[] { "EndpointId", "DataItemKey", "Direction", "Value" });
            migrationBuilder.CreateIndex(name: "IX_InterfaceUnmappedValues_ResolvedUtc", table: "InterfaceUnmappedValues", column: "ResolvedUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InterfaceUnmappedValues");
            migrationBuilder.DropIndex(
                name: "IX_InterfaceValueTranslations_EndpointId_DataItemKey_InternalValue_ExternalValue_Direction",
                table: "InterfaceValueTranslations");
            migrationBuilder.DropColumn(name: "EndpointId", table: "InterfaceValueTranslations");
            migrationBuilder.CreateIndex(
                name: "IX_InterfaceValueTranslations_DataItemKey_InternalValue_ExternalValue_Direction",
                table: "InterfaceValueTranslations",
                columns: new[] { "DataItemKey", "InternalValue", "ExternalValue", "Direction" },
                unique: true);
        }
    }
}
