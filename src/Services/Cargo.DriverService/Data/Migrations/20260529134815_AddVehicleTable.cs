using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cargo.DriverService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VehicleColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManufactureYear = table.Column<int>(type: "integer", nullable: false),
                    LicensePlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsLicenseVerified = table.Column<bool>(type: "boolean", nullable: false),
                    LicenseObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LicenseContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LicenseUploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LicenseReviewStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LicenseReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LicenseReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LicenseReviewedByKeycloakId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_DriverProfiles_DriverId",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DriverId",
                table: "Vehicles",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VehicleNumber",
                table: "Vehicles",
                column: "VehicleNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vehicles");
        }
    }
}
