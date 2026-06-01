using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cargo.DriverService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingDriverFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentVehicleNumber",
                table: "DriverProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverSsn",
                table: "DriverProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverStatus",
                table: "DriverProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Suspended");

            migrationBuilder.AddColumn<decimal>(
                name: "Rating",
                table: "DriverProfiles",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: false,
                defaultValue: 0.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WalletBalance",
                table: "DriverProfiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentVehicleNumber",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "DriverSsn",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "DriverStatus",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "WalletBalance",
                table: "DriverProfiles");
        }
    }
}
