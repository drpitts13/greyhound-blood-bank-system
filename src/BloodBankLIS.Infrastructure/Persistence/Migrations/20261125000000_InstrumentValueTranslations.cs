using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Per-instrument translation tables (order and result test codes, subtest codes, result values, interpretations,
    /// error codes). The old assay/analyte map is copied in and left in place, unused, until it is dropped. Unmapped
    /// values can name the instrument they came from.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261125000000_InstrumentValueTranslations")]
    public class InstrumentValueTranslations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstrumentValueTranslations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    InterfaceId = table.Column<long>(type: "bigint", nullable: false),
                    DataItemKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    InstrumentValue = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LocalValue = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Context = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentValueTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentValueTranslations_InstrumentInterfaces_InterfaceId",
                        column: x => x.InterfaceId,
                        principalTable: "InstrumentInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(
                name: "IX_InstrumentValueTranslations_InterfaceId_DataItemKey_Context_InstrumentValue_LocalValue_Direction",
                table: "InstrumentValueTranslations",
                columns: new[] { "InterfaceId", "DataItemKey", "Context", "InstrumentValue", "LocalValue", "Direction" },
                unique: true,
                filter: null);

            migrationBuilder.Sql(CopyTestMappingsSql);

            migrationBuilder.AddColumn<long>(name: "InstrumentInterfaceId", table: "InterfaceUnmappedValues", type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_InterfaceUnmappedValues_InstrumentInterfaceId", table: "InterfaceUnmappedValues", column: "InstrumentInterfaceId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_InterfaceUnmappedValues_InstrumentInterfaceId", table: "InterfaceUnmappedValues");
            migrationBuilder.DropColumn(name: "InstrumentInterfaceId", table: "InterfaceUnmappedValues");
            migrationBuilder.DropTable(name: "InstrumentValueTranslations");
        }

        /// <summary>
        /// Assay rows received (Inbound or Both) become result test codes, assay rows sent (Outbound or Both) become order
        /// test codes, and analyte rows become subtest codes. Shared with the development SQLite upgrade.
        /// </summary>
        public const string CopyTestMappingsSql =
            """
            INSERT INTO InstrumentValueTranslations (InterfaceId, DataItemKey, InstrumentValue, LocalValue, Context, Direction, SortOrder, CreatedUtc, CreatedBy)
            SELECT InterfaceId, 'Instrument.ResultTestCode', InstrumentCode, ApplicationCode, NULL, 0, MIN(SortOrder), MIN(CreatedUtc), MIN(CreatedBy)
            FROM InstrumentTestMappings WHERE Kind = 0 AND Direction IN (0, 2)
            GROUP BY InterfaceId, InstrumentCode, ApplicationCode;
            INSERT INTO InstrumentValueTranslations (InterfaceId, DataItemKey, InstrumentValue, LocalValue, Context, Direction, SortOrder, CreatedUtc, CreatedBy)
            SELECT InterfaceId, 'Instrument.OrderTestCode', InstrumentCode, ApplicationCode, NULL, 1, MIN(SortOrder), MIN(CreatedUtc), MIN(CreatedBy)
            FROM InstrumentTestMappings WHERE Kind = 0 AND Direction IN (1, 2)
            GROUP BY InterfaceId, InstrumentCode, ApplicationCode;
            INSERT INTO InstrumentValueTranslations (InterfaceId, DataItemKey, InstrumentValue, LocalValue, Context, Direction, SortOrder, CreatedUtc, CreatedBy)
            SELECT InterfaceId, 'Instrument.SubtestCode', InstrumentCode, ApplicationCode, NULL, 0, MIN(SortOrder), MIN(CreatedUtc), MIN(CreatedBy)
            FROM InstrumentTestMappings WHERE Kind = 1
            GROUP BY InterfaceId, InstrumentCode, ApplicationCode;
            """;
    }
}
