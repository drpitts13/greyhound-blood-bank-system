using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Transport containers (none shipped) and MTP pack records; each packed issue links to its pack.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103110000_MtpPackRecords")]
    public class MtpPackRecords : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransportContainers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContainerType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MinTemperatureC = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    MaxTemperatureC = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    ValidatedHours = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportContainers", x => x.Id);
                });
            migrationBuilder.CreateIndex(name: "IX_TransportContainers_Code", table: "TransportContainers", column: "Code", unique: true);

            migrationBuilder.CreateTable(
                name: "MtpPackRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MtpActivationId = table.Column<long>(type: "bigint", nullable: false),
                    PackNumber = table.Column<int>(type: "int", nullable: false),
                    TransportContainerId = table.Column<long>(type: "bigint", nullable: false),
                    ContainerLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemperatureC = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false),
                    TemperatureOutsideRange = table.Column<bool>(type: "bit", nullable: false),
                    PackedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PackedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MtpPackRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MtpPackRecords_MtpActivations_MtpActivationId",
                        column: x => x.MtpActivationId,
                        principalTable: "MtpActivations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MtpPackRecords_TransportContainers_TransportContainerId",
                        column: x => x.TransportContainerId,
                        principalTable: "TransportContainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_MtpPackRecords_MtpActivationId_PackNumber", table: "MtpPackRecords", columns: new[] { "MtpActivationId", "PackNumber" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_MtpPackRecords_TransportContainerId", table: "MtpPackRecords", column: "TransportContainerId");

            migrationBuilder.AddColumn<long>(name: "MtpPackRecordId", table: "Issues", type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Issues_MtpPackRecordId", table: "Issues", column: "MtpPackRecordId");
            migrationBuilder.AddForeignKey(
                name: "FK_Issues_MtpPackRecords_MtpPackRecordId",
                table: "Issues",
                column: "MtpPackRecordId",
                principalTable: "MtpPackRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Issues_MtpPackRecords_MtpPackRecordId", table: "Issues");
            migrationBuilder.DropIndex(name: "IX_Issues_MtpPackRecordId", table: "Issues");
            migrationBuilder.DropColumn(name: "MtpPackRecordId", table: "Issues");
            migrationBuilder.DropTable(name: "MtpPackRecords");
            migrationBuilder.DropTable(name: "TransportContainers");
        }
    }
}
