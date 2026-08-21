using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHousekeepingScheduleOffsets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "housekeeping_after_check_out_minutes",
                table: "properties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "housekeeping_before_check_in_minutes",
                table: "properties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_properties_housekeeping_after_check_out_minutes",
                table: "properties",
                sql: "housekeeping_after_check_out_minutes BETWEEN 0 AND 1440");

            migrationBuilder.AddCheckConstraint(
                name: "ck_properties_housekeeping_before_check_in_minutes",
                table: "properties",
                sql: "housekeeping_before_check_in_minutes BETWEEN 0 AND 1440");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_properties_housekeeping_after_check_out_minutes",
                table: "properties");

            migrationBuilder.DropCheckConstraint(
                name: "ck_properties_housekeeping_before_check_in_minutes",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "housekeeping_after_check_out_minutes",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "housekeeping_before_check_in_minutes",
                table: "properties");
        }
    }
}
