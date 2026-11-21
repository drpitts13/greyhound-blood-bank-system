using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 84: the sample ID prefixes of an instrument's own QC samples.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270124000000_InstrumentQcSamplePrefixes")]
    public class InstrumentQcSamplePrefixes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "QcSampleIdPrefixes", table: "InstrumentInterfaces", type: "nvarchar(200)", maxLength: 200, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "QcSampleIdPrefixes", table: "InstrumentInterfaces");
        }
    }
}
