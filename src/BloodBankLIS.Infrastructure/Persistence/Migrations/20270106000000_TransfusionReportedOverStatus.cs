using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-083 option B: a transfusion the EHR reported over a unit's status, and its review (Cycle 700).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270106000000_TransfusionReportedOverStatus")]
    public class TransfusionReportedOverStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "UnitStatusAtReport", table: "TransfusionEvents", type: "int", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ReportReviewedUtc", table: "TransfusionEvents", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReportReviewedBy", table: "TransfusionEvents", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReportReviewNote", table: "TransfusionEvents", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "TransfusionReportedUtc", table: "BloodProducts", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UnitStatusAtReport", table: "TransfusionEvents");
            migrationBuilder.DropColumn(name: "ReportReviewedUtc", table: "TransfusionEvents");
            migrationBuilder.DropColumn(name: "ReportReviewedBy", table: "TransfusionEvents");
            migrationBuilder.DropColumn(name: "ReportReviewNote", table: "TransfusionEvents");
            migrationBuilder.DropColumn(name: "TransfusionReportedUtc", table: "BloodProducts");
        }
    }
}
