using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConsecutiveSlotBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "full_day_price",
                table: "rooms",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "full_day_pricing_enabled",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "booking_rate_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_rate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    check_in_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    check_out_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    rate_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    list_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    applied_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    pricing_rule = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_booking_rate_segments", x => x.id);
                    table.CheckConstraint("ck_booking_rate_segments_amounts", "list_price >= 0 AND applied_amount >= 0");
                    table.CheckConstraint("ck_booking_rate_segments_interval", "check_out_utc > check_in_utc");
                    table.ForeignKey(
                        name: "f_k_booking_rate_segments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_booking_rate_segments_room_rates_room_rate_id",
                        column: x => x.room_rate_id,
                        principalTable: "room_rates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_booking_rate_segments_booking_id_sort_order",
                table: "booking_rate_segments",
                columns: new[] { "booking_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_booking_rate_segments_room_rate_id_service_date",
                table: "booking_rate_segments",
                columns: new[] { "room_rate_id", "service_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_rate_segments");

            migrationBuilder.DropColumn(
                name: "full_day_price",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "full_day_pricing_enabled",
                table: "rooms");
        }
    }
}
