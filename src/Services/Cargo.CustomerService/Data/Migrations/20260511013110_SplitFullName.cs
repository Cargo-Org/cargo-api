using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cargo.CustomerService.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitFullName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "CustomerProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "CustomerProfiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "CustomerProfiles");
        }
    }
}
