using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// OCD-088 (decided 2026-11-14, option C): each HL7 interface says how its times without an offset are read and how
    /// its times are sent. Existing interfaces keep UTC (0), the convention they were validated with.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270122000000_InterfaceTimestampConvention")]
    public class InterfaceTimestampConvention : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "TimestampConvention", table: "InterfaceEndpoints", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TimestampConvention", table: "InterfaceEndpoints");
        }
    }
}
