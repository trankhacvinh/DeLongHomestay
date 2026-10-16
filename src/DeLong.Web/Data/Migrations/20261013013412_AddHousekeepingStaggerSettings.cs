using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHousekeepingStaggerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "housekeeping_cleaning_minutes",
                table: "properties",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "housekeeping_max_advance_minutes",
                table: "properties",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "housekeeping_stagger_enabled",
                table: "properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "housekeeping_work_end",
                table: "properties",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "housekeeping_work_start",
                table: "properties",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "housekeeping_worker_count",
                table: "properties",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "housekeeping_cleaning_minutes",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "housekeeping_max_advance_minutes",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "housekeeping_stagger_enabled",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "housekeeping_work_end",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "housekeeping_work_start",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "housekeeping_worker_count",
                table: "properties");
        }
    }
}
