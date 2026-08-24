using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InterfaceSetupMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InterfaceType",
                table: "InterfaceEndpoints",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MappingMode",
                table: "InterfaceEndpoints",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "VendorCode",
                table: "InterfaceEndpoints",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InterfaceFieldMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EndpointId = table.Column<long>(type: "bigint", nullable: false),
                    DataItemKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Hl7Path = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterfaceFieldMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterfaceFieldMappings_InterfaceEndpoints_EndpointId",
                        column: x => x.EndpointId,
                        principalTable: "InterfaceEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceFieldMappings_EndpointId_DataItemKey",
                table: "InterfaceFieldMappings",
                columns: new[] { "EndpointId", "DataItemKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterfaceFieldMappings");

            migrationBuilder.DropColumn(
                name: "InterfaceType",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "MappingMode",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "VendorCode",
                table: "InterfaceEndpoints");
        }
    }
}
