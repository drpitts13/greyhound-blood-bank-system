using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Re-installs the report views so <c>rpt_crossmatch_transfusion</c> has <c>occurred_month</c>, for the
    /// crossmatches and transfusions by month report (end-user testing pass 11). Development SQLite installs the views
    /// from the seeder.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270112000000_CrossmatchTransfusionMonth")]
    public class CrossmatchTransfusionMonth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ReportViewSql.Apply(sql => migrationBuilder.Sql(sql), migrationBuilder.ActiveProvider);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The earlier view definition is restored by re-running the earlier migration's Apply; nothing to drop.
        }
    }
}
