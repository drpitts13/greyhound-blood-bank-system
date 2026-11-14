using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-073: how an outside antibody report is documented, and its confirmation here (Cycle 669).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261230000000_OutsideAntibodyConfirmation")]
    public class OutsideAntibodyConfirmation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ReportKind", table: "AntibodyHistory", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReportDocumentation", table: "AntibodyHistory", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ConfirmedUtc", table: "AntibodyHistory", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ConfirmedBy", table: "AntibodyHistory", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ConfirmationNote", table: "AntibodyHistory", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReportKind", table: "AntibodyHistory");
            migrationBuilder.DropColumn(name: "ReportDocumentation", table: "AntibodyHistory");
            migrationBuilder.DropColumn(name: "ConfirmedUtc", table: "AntibodyHistory");
            migrationBuilder.DropColumn(name: "ConfirmedBy", table: "AntibodyHistory");
            migrationBuilder.DropColumn(name: "ConfirmationNote", table: "AntibodyHistory");
        }
    }
}
