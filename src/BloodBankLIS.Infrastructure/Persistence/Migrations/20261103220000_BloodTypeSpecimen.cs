using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Records the specimen behind each blood type so a second determination must come from another tube.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103220000_BloodTypeSpecimen")]
    public class BloodTypeSpecimen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(name: "SpecimenId", table: "PatientBloodTypeHistory", type: "bigint", nullable: true);
            migrationBuilder.Sql(BloodTypeSpecimenBackfill.Sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SpecimenId", table: "PatientBloodTypeHistory");
        }
    }
}
