using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingV2MultiDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "room_rates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TimeSlot");

            // Existing preset rows already carry the legacy IsOvernight flag.
            // Preserve their meaning while introducing the explicit rate type.
            migrationBuilder.Sql("""
                UPDATE room_rates
                SET type = 'Overnight'
                WHERE is_overnight = TRUE;
                """);

            migrationBuilder.AddColumn<int>(
                name: "night_count",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rate_name",
                table: "bookings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "room_rate_id",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "bookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TimeSlot");

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price",
                table: "bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_bookings_room_rate_id",
                table: "bookings",
                column: "room_rate_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_bookings_room_rates_room_rate_id",
                table: "bookings",
                column: "room_rate_id",
                principalTable: "room_rates",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_bookings_room_rates_room_rate_id",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "i_x_bookings_room_rate_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "type",
                table: "room_rates");

            migrationBuilder.DropColumn(
                name: "night_count",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "rate_name",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "room_rate_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "type",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "unit_price",
                table: "bookings");
        }
    }
}
