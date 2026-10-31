using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261031220000_UserTotp")]
    public class UserTotp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "TotpSecretProtected", table: "Users", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
            migrationBuilder.AddColumn<string>(name: "TotpPendingSecretProtected", table: "Users", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "TotpEnrolledUtc", table: "Users", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<long>(name: "TotpLastStep", table: "Users", type: "bigint", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TotpSecretProtected", table: "Users");
            migrationBuilder.DropColumn(name: "TotpPendingSecretProtected", table: "Users");
            migrationBuilder.DropColumn(name: "TotpEnrolledUtc", table: "Users");
            migrationBuilder.DropColumn(name: "TotpLastStep", table: "Users");
        }
    }
}
