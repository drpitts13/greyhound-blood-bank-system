using System;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A reaction investigation records the bag culture and the notification of the blood supplier (end-user testing
    /// pass 19).
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270114000000_ReactionCultureAndSupplier")]
    public class ReactionCultureAndSupplier : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "BagCultureSent", table: "ReactionInvestigations", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "BagCultureResult", table: "ReactionInvestigations", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "SupplierNotifiedUtc", table: "ReactionInvestigations", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "SupplierNotificationNote", table: "ReactionInvestigations", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BagCultureSent", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "BagCultureResult", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "SupplierNotifiedUtc", table: "ReactionInvestigations");
            migrationBuilder.DropColumn(name: "SupplierNotificationNote", table: "ReactionInvestigations");
        }
    }
}
