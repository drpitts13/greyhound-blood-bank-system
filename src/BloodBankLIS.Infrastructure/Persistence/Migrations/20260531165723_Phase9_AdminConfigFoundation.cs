using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9_AdminConfigFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsServiceAccount",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ProductTypes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultChargeCode",
                table: "ProductTypes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Isbt128ProductCode",
                table: "ProductTypes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssueRules",
                table: "ProductTypes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificationRules",
                table: "ProductTypes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAboMatch",
                table: "ProductTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresRhMatch",
                table: "ProductTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReturnRules",
                table: "ProductTypes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageRequirements",
                table: "ProductTypes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ProductTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AckTimeoutSeconds",
                table: "InterfaceEndpoints",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "InterfaceEndpoints",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetryCount",
                table: "InterfaceEndpoints",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageLoggingLevel",
                table: "InterfaceEndpoints",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivingApplication",
                table: "InterfaceEndpoints",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivingFacility",
                table: "InterfaceEndpoints",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReplayAllowed",
                table: "InterfaceEndpoints",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RetryDelaySeconds",
                table: "InterfaceEndpoints",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SendingApplication",
                table: "InterfaceEndpoints",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SendingFacility",
                table: "InterfaceEndpoints",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "InterfaceEndpoints",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "AuditEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDevMode",
                table: "AuditEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConfigurationChangeHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OldValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Workstation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChangedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsDevMode = table.Column<bool>(type: "bit", nullable: false),
                    SignatureId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationChangeHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ResultValueType = table.Column<int>(type: "int", nullable: false),
                    AllowedResultValues = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequiredSpecimenType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TestingMethod = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PerformingDepartment = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Billable = table.Column<bool>(type: "bit", nullable: false),
                    ChargeCodeMapping = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VerificationRequired = table.Column<bool>(type: "bit", nullable: false),
                    ContributesToAboRhHistory = table.Column<bool>(type: "bit", nullable: false),
                    ContributesToAntibodyHistory = table.Column<bool>(type: "bit", nullable: false),
                    ContributesToCompatibility = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    IsPendingApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributeAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductTypeId = table.Column<long>(type: "bigint", nullable: false),
                    ProductAttributeId = table.Column<long>(type: "bigint", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributeAssignments_ProductAttributes_ProductAttributeId",
                        column: x => x.ProductAttributeId,
                        principalTable: "ProductAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductAttributeAssignments_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationChangeHistory_ChangedUtc",
                table: "ConfigurationChangeHistory",
                column: "ChangedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationChangeHistory_EntityType_EntityId",
                table: "ConfigurationChangeHistory",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeAssignments_ProductAttributeId",
                table: "ProductAttributeAssignments",
                column: "ProductAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeAssignments_ProductTypeId_ProductAttributeId",
                table: "ProductAttributeAssignments",
                columns: new[] { "ProductTypeId", "ProductAttributeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributes_Code",
                table: "ProductAttributes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_Category",
                table: "TestDefinitions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_Code",
                table: "TestDefinitions",
                column: "Code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigurationChangeHistory");

            migrationBuilder.DropTable(
                name: "ProductAttributeAssignments");

            migrationBuilder.DropTable(
                name: "TestDefinitions");

            migrationBuilder.DropTable(
                name: "ProductAttributes");

            migrationBuilder.DropColumn(
                name: "IsServiceAccount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "DefaultChargeCode",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "Isbt128ProductCode",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "IssueRules",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "ModificationRules",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "RequiresAboMatch",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "RequiresRhMatch",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "ReturnRules",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "StorageRequirements",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "AckTimeoutSeconds",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "MaxRetryCount",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "MessageLoggingLevel",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "ReceivingApplication",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "ReceivingFacility",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "ReplayAllowed",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "RetryDelaySeconds",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "SendingApplication",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "SendingFacility",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "InterfaceEndpoints");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IsDevMode",
                table: "AuditEvents");
        }
    }
}
