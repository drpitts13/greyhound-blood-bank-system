using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Where each tube is stored now, and every place it has been.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261208000000_SpecimenStorage")]
    public class SpecimenStorage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "StorageLocation", table: "Specimens", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "StoredUtc", table: "Specimens", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "StoredBy", table: "Specimens", type: "nvarchar(100)", maxLength: 100, nullable: true);

            migrationBuilder.CreateTable(
                name: "SpecimenStorageEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpecimenId = table.Column<long>(type: "bigint", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreviousLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StoredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StoredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_SpecimenStorageEvents", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_SpecimenStorageEvents_SpecimenId", table: "SpecimenStorageEvents", column: "SpecimenId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SpecimenStorageEvents");
            migrationBuilder.DropColumn(name: "StoredBy", table: "Specimens");
            migrationBuilder.DropColumn(name: "StoredUtc", table: "Specimens");
            migrationBuilder.DropColumn(name: "StorageLocation", table: "Specimens");
        }
    }
}
