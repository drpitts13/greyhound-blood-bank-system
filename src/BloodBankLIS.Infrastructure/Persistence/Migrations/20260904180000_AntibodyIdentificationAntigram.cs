using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20260904180000_AntibodyIdentificationAntigram")]
    public partial class AntibodyIdentificationAntigram : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AntibodyPanelManufacturers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    IsPendingApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibodyPanelManufacturers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AntibodyPanelLots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManufacturerId = table.Column<long>(type: "bigint", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PanelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsSelectedCellLot = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibodyPanelLots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AntibodyPanelCells",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LotId = table.Column<long>(type: "bigint", nullable: false),
                    CellNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibodyPanelCells", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AntibodyPanelCellAntigens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CellId = table.Column<long>(type: "bigint", nullable: false),
                    BloodAttributeDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Expression = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibodyPanelCellAntigens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AntibodyIdentificationWorkups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    SpecimenId = table.Column<long>(type: "bigint", nullable: true),
                    SourceResultId = table.Column<long>(type: "bigint", nullable: true),
                    PrimaryLotId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DatResult = table.Column<int>(type: "int", nullable: false),
                    DatMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TechnologistInterpretation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TechnologistUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InterpretedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupervisorUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupervisorComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupervisorAccepted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibodyIdentificationWorkups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AntibodyIdentificationWorkupLots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkupId = table.Column<long>(type: "bigint", nullable: false),
                    LotId = table.Column<long>(type: "bigint", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibodyIdentificationWorkupLots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AntibodyIdentificationReactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkupId = table.Column<long>(type: "bigint", nullable: false),
                    CellId = table.Column<long>(type: "bigint", nullable: false),
                    PhaseCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Strength = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibodyIdentificationReactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AntibodyIdentificationFindings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkupId = table.Column<long>(type: "bigint", nullable: false),
                    BloodAttributeDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    Specificity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PostedToHistory = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntibodyIdentificationFindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyPanelManufacturers_Code",
                table: "AntibodyPanelManufacturers",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyPanelLots_ManufacturerId_LotNumber",
                table: "AntibodyPanelLots",
                columns: new[] { "ManufacturerId", "LotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyPanelLots_ExpiresOn",
                table: "AntibodyPanelLots",
                column: "ExpiresOn");

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyPanelLots_IsActive",
                table: "AntibodyPanelLots",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyPanelCells_LotId_CellNumber",
                table: "AntibodyPanelCells",
                columns: new[] { "LotId", "CellNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyPanelCellAntigens_CellId_BloodAttributeDefinitionId",
                table: "AntibodyPanelCellAntigens",
                columns: new[] { "CellId", "BloodAttributeDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyIdentificationWorkups_PatientId",
                table: "AntibodyIdentificationWorkups",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyIdentificationWorkups_Status",
                table: "AntibodyIdentificationWorkups",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyIdentificationWorkupLots_WorkupId_LotId",
                table: "AntibodyIdentificationWorkupLots",
                columns: new[] { "WorkupId", "LotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyIdentificationReactions_WorkupId_CellId_PhaseCode",
                table: "AntibodyIdentificationReactions",
                columns: new[] { "WorkupId", "CellId", "PhaseCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntibodyIdentificationFindings_WorkupId",
                table: "AntibodyIdentificationFindings",
                column: "WorkupId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AntibodyIdentificationFindings");
            migrationBuilder.DropTable(name: "AntibodyIdentificationReactions");
            migrationBuilder.DropTable(name: "AntibodyIdentificationWorkupLots");
            migrationBuilder.DropTable(name: "AntibodyIdentificationWorkups");
            migrationBuilder.DropTable(name: "AntibodyPanelCellAntigens");
            migrationBuilder.DropTable(name: "AntibodyPanelCells");
            migrationBuilder.DropTable(name: "AntibodyPanelLots");
            migrationBuilder.DropTable(name: "AntibodyPanelManufacturers");
        }
    }
}
