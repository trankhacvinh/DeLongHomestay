using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "manager_cancellation_email_enabled",
                table: "property_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "booking_cancellations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    total_amount_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_due_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    restored_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_backfilled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_booking_cancellations", x => x.id);
                    table.ForeignKey(
                        name: "f_k_booking_cancellations_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_booking_cancellations_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_booking_cancellations_property_id_cancelled_at_utc",
                table: "booking_cancellations",
                columns: new[] { "property_id", "cancelled_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_booking_cancellations_active_booking",
                table: "booking_cancellations",
                column: "booking_id",
                unique: true,
                filter: "restored_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_cancellations");

            migrationBuilder.DropColumn(
                name: "manager_cancellation_email_enabled",
                table: "property_notification_settings");
        }
    }
}
