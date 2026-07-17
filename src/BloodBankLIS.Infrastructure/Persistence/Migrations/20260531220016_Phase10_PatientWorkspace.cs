using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10_PatientWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EncounterId",
                table: "Specimens",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EncounterId",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "FillerOrderNumber",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FulfillmentStatus",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderCategory",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OrderName",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrderedByUser",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OrderingLocationId",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ProductTypeId",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultStatus",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSystem",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TestCode",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EncounterId",
                table: "Issues",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OrderId",
                table: "Issues",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EncounterId",
                table: "Allocations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OrderId",
                table: "Allocations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Encounters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    VisitNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EncounterType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdmitUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DischargeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttendingProvider = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AdmissionLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CurrentLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DischargeDisposition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FinancialClass = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExternalVisitId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Encounters_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderingLocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Hl7MappingCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderingLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderSpecimens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    SpecimenId = table.Column<long>(type: "bigint", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSpecimens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderSpecimens_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderSpecimens_Specimens_SpecimenId",
                        column: x => x.SpecimenId,
                        principalTable: "Specimens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_EncounterId",
                table: "Specimens",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EncounterId",
                table: "Orders",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderedUtc",
                table: "Orders",
                column: "OrderedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderingLocationId",
                table: "Orders",
                column: "OrderingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ProductTypeId",
                table: "Orders",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_EncounterId",
                table: "Issues",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_OrderId",
                table: "Issues",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_EncounterId",
                table: "Allocations",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_OrderId",
                table: "Allocations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_PatientId",
                table: "Encounters",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_Status",
                table: "Encounters",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_VisitNumber",
                table: "Encounters",
                column: "VisitNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderingLocations_Code",
                table: "OrderingLocations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSpecimens_OrderId_SpecimenId",
                table: "OrderSpecimens",
                columns: new[] { "OrderId", "SpecimenId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSpecimens_SpecimenId",
                table: "OrderSpecimens",
                column: "SpecimenId");

            // Backfill reference data and link existing orders before FK enforcement.
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("""
                    INSERT INTO OrderingLocations (Code, Name, IsActive, CreatedUtc, CreatedBy)
                    SELECT 'LEGACY', 'Legacy Ordering Location', 1, '2026-05-31T12:00:00', 'migration'
                    WHERE NOT EXISTS (SELECT 1 FROM OrderingLocations WHERE Code = 'LEGACY');

                    INSERT INTO Encounters (PatientId, VisitNumber, EncounterType, Status, CreatedUtc, CreatedBy)
                    SELECT DISTINCT o.PatientId, 'LEGACY-' + CONVERT(varchar(20), o.PatientId), 99, 4, '2026-05-31T12:00:00', 'migration'
                    FROM Orders o
                    WHERE NOT EXISTS (
                        SELECT 1 FROM Encounters e
                        WHERE e.PatientId = o.PatientId AND e.VisitNumber = 'LEGACY-' + CONVERT(varchar(20), o.PatientId));

                    UPDATE o SET
                        EncounterId = e.Id,
                        OrderingLocationId = loc.Id,
                        OrderName = CASE WHEN o.OrderName = '' THEN 'Legacy order' ELSE o.OrderName END
                    FROM Orders o
                    INNER JOIN Encounters e ON e.PatientId = o.PatientId AND e.VisitNumber = 'LEGACY-' + CONVERT(varchar(20), o.PatientId)
                    CROSS JOIN (SELECT TOP 1 Id FROM OrderingLocations WHERE Code = 'LEGACY') loc
                    WHERE o.EncounterId = 0 OR o.OrderingLocationId = 0;

                    INSERT INTO OrderSpecimens (OrderId, SpecimenId, IsPrimary, CreatedUtc, CreatedBy)
                    SELECT DISTINCT tr.OrderId, tr.SpecimenId, 1, '2026-05-31T12:00:00', 'migration'
                    FROM TestResults tr
                    WHERE tr.OrderId IS NOT NULL
                      AND NOT EXISTS (
                        SELECT 1 FROM OrderSpecimens os
                        WHERE os.OrderId = tr.OrderId AND os.SpecimenId = tr.SpecimenId);
                    """);
            }
            else
            {
                migrationBuilder.Sql("""
                    INSERT INTO OrderingLocations (Code, Name, IsActive, CreatedUtc, CreatedBy)
                    SELECT 'LEGACY', 'Legacy Ordering Location', 1, '2026-05-31T12:00:00', 'migration'
                    WHERE NOT EXISTS (SELECT 1 FROM OrderingLocations WHERE Code = 'LEGACY');

                    INSERT INTO Encounters (PatientId, VisitNumber, EncounterType, Status, CreatedUtc, CreatedBy)
                    SELECT DISTINCT o.PatientId, 'LEGACY-' || o.PatientId, 99, 4, '2026-05-31T12:00:00', 'migration'
                    FROM Orders o
                    WHERE NOT EXISTS (
                        SELECT 1 FROM Encounters e
                        WHERE e.PatientId = o.PatientId AND e.VisitNumber = 'LEGACY-' || o.PatientId);

                    UPDATE Orders SET
                        EncounterId = (
                            SELECT e.Id FROM Encounters e
                            WHERE e.PatientId = Orders.PatientId AND e.VisitNumber = 'LEGACY-' || Orders.PatientId),
                        OrderingLocationId = (SELECT Id FROM OrderingLocations WHERE Code = 'LEGACY' LIMIT 1),
                        OrderName = CASE WHEN OrderName = '' THEN 'Legacy order' ELSE OrderName END
                    WHERE EncounterId = 0 OR OrderingLocationId = 0;

                    INSERT INTO OrderSpecimens (OrderId, SpecimenId, IsPrimary, CreatedUtc, CreatedBy)
                    SELECT DISTINCT tr.OrderId, tr.SpecimenId, 1, '2026-05-31T12:00:00', 'migration'
                    FROM TestResults tr
                    WHERE tr.OrderId IS NOT NULL
                      AND NOT EXISTS (
                        SELECT 1 FROM OrderSpecimens os
                        WHERE os.OrderId = tr.OrderId AND os.SpecimenId = tr.SpecimenId);
                    """);
            }

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Encounters_EncounterId",
                table: "Orders",
                column: "EncounterId",
                principalTable: "Encounters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_OrderingLocations_OrderingLocationId",
                table: "Orders",
                column: "OrderingLocationId",
                principalTable: "OrderingLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_ProductTypes_ProductTypeId",
                table: "Orders",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Specimens_Encounters_EncounterId",
                table: "Specimens",
                column: "EncounterId",
                principalTable: "Encounters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Encounters_EncounterId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_OrderingLocations_OrderingLocationId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_ProductTypes_ProductTypeId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Specimens_Encounters_EncounterId",
                table: "Specimens");

            migrationBuilder.DropTable(
                name: "Encounters");

            migrationBuilder.DropTable(
                name: "OrderingLocations");

            migrationBuilder.DropTable(
                name: "OrderSpecimens");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_EncounterId",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Orders_EncounterId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderedUtc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderingLocationId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ProductTypeId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Issues_EncounterId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_OrderId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Allocations_EncounterId",
                table: "Allocations");

            migrationBuilder.DropIndex(
                name: "IX_Allocations_OrderId",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "EncounterId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EncounterId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FillerOrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderCategory",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderedByUser",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderingLocationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProductTypeId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ResultStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SourceSystem",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TestCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EncounterId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "EncounterId",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Allocations");
        }
    }
}
