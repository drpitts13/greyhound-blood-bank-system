using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_Hl7Interfaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HL7Messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EndpointId = table.Column<long>(type: "bigint", nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggerEvent = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MessageControlId = table.Column<string>(type: "nvarchar(199)", maxLength: 199, nullable: false),
                    RawMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParsedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AckCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HL7Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterfaceEndpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Transport = table.Column<int>(type: "int", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Port = table.Column<int>(type: "int", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MessageTypes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MappingProfile = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterfaceEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterfaceErrorQueue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Hl7MessageId = table.Column<long>(type: "bigint", nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ErrorDetail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NextRetryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Resolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResolvedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterfaceErrorQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterfaceErrorQueue_HL7Messages_Hl7MessageId",
                        column: x => x.Hl7MessageId,
                        principalTable: "HL7Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HL7Messages_MessageControlId",
                table: "HL7Messages",
                column: "MessageControlId");

            migrationBuilder.CreateIndex(
                name: "IX_HL7Messages_MessageType",
                table: "HL7Messages",
                column: "MessageType");

            migrationBuilder.CreateIndex(
                name: "IX_HL7Messages_ReceivedUtc",
                table: "HL7Messages",
                column: "ReceivedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HL7Messages_Status",
                table: "HL7Messages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceEndpoints_Name",
                table: "InterfaceEndpoints",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceErrorQueue_Hl7MessageId",
                table: "InterfaceErrorQueue",
                column: "Hl7MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceErrorQueue_NextRetryUtc",
                table: "InterfaceErrorQueue",
                column: "NextRetryUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceErrorQueue_Resolved",
                table: "InterfaceErrorQueue",
                column: "Resolved");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterfaceEndpoints");

            migrationBuilder.DropTable(
                name: "InterfaceErrorQueue");

            migrationBuilder.DropTable(
                name: "HL7Messages");
        }
    }
}
