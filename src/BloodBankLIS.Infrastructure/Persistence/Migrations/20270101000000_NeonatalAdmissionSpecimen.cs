using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-074: an infant's specimen valid for the admission keeps its usual expiration here (Cycle 672).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270101000000_NeonatalAdmissionSpecimen")]
    public class NeonatalAdmissionSpecimen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "NeonatalStandardExpiresUtc", table: "Specimens", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NeonatalStandardExpiresUtc", table: "Specimens");
        }
    }
}
