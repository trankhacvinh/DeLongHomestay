using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekendAndSpecialDayPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "use_weekday_full_day_price_on_weekend",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weekend_full_day_price",
                table: "rooms",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "use_weekday_price_on_weekend",
                table: "room_rates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weekend_price",
                table: "room_rates",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "special_surcharge_amount",
                table: "bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "combo_discount_amount",
                table: "booking_rate_segments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "combo_discount_percent",
                table: "booking_rate_segments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "day_profile",
                table: "booking_rate_segments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Weekday");

            migrationBuilder.AddColumn<string>(
                name: "special_day_name",
                table: "booking_rate_segments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "special_pricing_day_id",
                table: "booking_rate_segments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "special_surcharge_amount",
                table: "booking_rate_segments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "special_surcharge_percent",
                table: "booking_rate_segments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "property_pricing_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    three_slot_discount_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    three_slot_count = table.Column<int>(type: "integer", nullable: false),
                    three_slot_discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    weekend_day_mask = table.Column<int>(type: "integer", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_pricing_settings", x => x.id);
                    table.CheckConstraint("ck_property_pricing_three_slot_count", "three_slot_count BETWEEN 2 AND 20");
                    table.CheckConstraint("ck_property_pricing_three_slot_discount", "three_slot_discount_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "f_k_property_pricing_settings_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "special_pricing_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    base_price_profile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    surcharge_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    booking_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    allow_three_slot_combo = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_special_pricing_days", x => x.id);
                    table.CheckConstraint("ck_special_pricing_days_range", "end_date >= start_date");
                    table.CheckConstraint("ck_special_pricing_days_surcharge", "surcharge_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "f_k_special_pricing_days_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_booking_rate_segments_special_pricing_day_id",
                table: "booking_rate_segments",
                column: "special_pricing_day_id");

            migrationBuilder.CreateIndex(
                name: "i_x_property_pricing_settings_property_id",
                table: "property_pricing_settings",
                column: "property_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_special_pricing_days_property_id_start_date_end_date",
                table: "special_pricing_days",
                columns: new[] { "property_id", "start_date", "end_date" });

            migrationBuilder.AddForeignKey(
                name: "f_k_booking_rate_segments_special_pricing_days_special_pricing_da~",
                table: "booking_rate_segments",
                column: "special_pricing_day_id",
                principalTable: "special_pricing_days",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_booking_rate_segments_special_pricing_days_special_pricing_da~",
                table: "booking_rate_segments");

            migrationBuilder.DropTable(
                name: "property_pricing_settings");

            migrationBuilder.DropTable(
                name: "special_pricing_days");

            migrationBuilder.DropIndex(
                name: "i_x_booking_rate_segments_special_pricing_day_id",
                table: "booking_rate_segments");

            migrationBuilder.DropColumn(
                name: "use_weekday_full_day_price_on_weekend",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "weekend_full_day_price",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "use_weekday_price_on_weekend",
                table: "room_rates");

            migrationBuilder.DropColumn(
                name: "weekend_price",
                table: "room_rates");

            migrationBuilder.DropColumn(
                name: "special_surcharge_amount",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "combo_discount_amount",
                table: "booking_rate_segments");

            migrationBuilder.DropColumn(
                name: "combo_discount_percent",
                table: "booking_rate_segments");

            migrationBuilder.DropColumn(
                name: "day_profile",
                table: "booking_rate_segments");

            migrationBuilder.DropColumn(
                name: "special_day_name",
                table: "booking_rate_segments");

            migrationBuilder.DropColumn(
                name: "special_pricing_day_id",
                table: "booking_rate_segments");

            migrationBuilder.DropColumn(
                name: "special_surcharge_amount",
                table: "booking_rate_segments");

            migrationBuilder.DropColumn(
                name: "special_surcharge_percent",
                table: "booking_rate_segments");
        }
    }
}
