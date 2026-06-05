using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cargo.CustomerService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "CustomerProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "CustomerProfiles");
        }
    }
}
