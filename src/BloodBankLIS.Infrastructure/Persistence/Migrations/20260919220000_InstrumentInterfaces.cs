using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260919220000_InstrumentInterfaces")]
    public partial class InstrumentInterfaces : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstrumentInterfaces",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_InstrumentInterfaces", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentInterfaces_Name",
                table: "InstrumentInterfaces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateTable(
                name: "InstrumentConnections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterfaceId = table.Column<long>(type: "bigint", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Port = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentConnections_InstrumentInterfaces_InterfaceId",
                        column: x => x.InterfaceId,
                        principalTable: "InstrumentInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentConnections_InterfaceId",
                table: "InstrumentConnections",
                column: "InterfaceId",
                unique: true);

            migrationBuilder.CreateTable(
                name: "InstrumentFieldMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterfaceId = table.Column<long>(type: "bigint", nullable: false),
                    DataItemKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AstmPath = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentFieldMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentFieldMappings_InstrumentInterfaces_InterfaceId",
                        column: x => x.InterfaceId,
                        principalTable: "InstrumentInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentFieldMappings_InterfaceId_DataItemKey",
                table: "InstrumentFieldMappings",
                columns: new[] { "InterfaceId", "DataItemKey" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "InstrumentTestMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterfaceId = table.Column<long>(type: "bigint", nullable: false),
                    InstrumentCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApplicationCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentTestMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentTestMappings_InstrumentInterfaces_InterfaceId",
                        column: x => x.InterfaceId,
                        principalTable: "InstrumentInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentInterfaceRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterfaceId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ConditionExpression = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActionExpression = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StopOnMatch = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentInterfaceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentInterfaceRules_InstrumentInterfaces_InterfaceId",
                        column: x => x.InterfaceId,
                        principalTable: "InstrumentInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterfaceId = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    MessageKind = table.Column<int>(type: "int", nullable: false),
                    RawMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParsedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MappedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RuleTraceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SpecimenId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PatientId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_InstrumentMessages", x => x.Id));

            migrationBuilder.CreateTable(
                name: "InstrumentInterfaceLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterfaceId = table.Column<long>(type: "bigint", nullable: false),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_InstrumentInterfaceLogs", x => x.Id));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InstrumentInterfaceLogs");
            migrationBuilder.DropTable(name: "InstrumentMessages");
            migrationBuilder.DropTable(name: "InstrumentInterfaceRules");
            migrationBuilder.DropTable(name: "InstrumentTestMappings");
            migrationBuilder.DropTable(name: "InstrumentFieldMappings");
            migrationBuilder.DropTable(name: "InstrumentConnections");
            migrationBuilder.DropTable(name: "InstrumentInterfaces");
        }
    }
}
