using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomConditionReportRatingAndVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rating",
                table: "room_condition_reports",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<bool>(
                name: "is_video",
                table: "room_condition_report_images",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_room_condition_reports_rating",
                table: "room_condition_reports",
                sql: "rating BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_room_condition_reports_rating",
                table: "room_condition_reports");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "room_condition_reports");

            migrationBuilder.DropColumn(
                name: "is_video",
                table: "room_condition_report_images");
        }
    }
}
