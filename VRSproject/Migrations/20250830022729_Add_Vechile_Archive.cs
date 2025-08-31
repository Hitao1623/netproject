using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VRSproject.Migrations
{
    /// <inheritdoc />
    public partial class Add_Vechile_Archive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivdReason",
                table: "Vehicles",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "Vehicles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "Vehicles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "RateMultiplier",
                table: "PricingPlans",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_isArchived_Status_VehicleType",
                table: "Vehicles",
                columns: new[] { "isArchived", "Status", "VehicleType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_isArchived_Status_VehicleType",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ArchivdReason",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "Vehicles");

            migrationBuilder.AlterColumn<decimal>(
                name: "RateMultiplier",
                table: "PricingPlans",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,4)",
                oldPrecision: 10,
                oldScale: 4);
        }
    }
}
