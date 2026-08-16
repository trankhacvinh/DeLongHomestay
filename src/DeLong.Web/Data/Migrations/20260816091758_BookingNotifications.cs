using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class BookingNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "property_notification_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    in_app_booking_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    email_booking_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    email_recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    smtp_host = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    smtp_port = table.Column<int>(type: "integer", nullable: false),
                    smtp_use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    smtp_username = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    smtp_password_protected = table.Column<string>(type: "text", nullable: true),
                    smtp_from_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    smtp_from_name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    last_email_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    last_email_error_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_email_sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_notification_settings", x => x.id);
                    table.ForeignKey(
                        name: "f_k_property_notification_settings_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "property_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    message = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    action_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_notifications", x => x.id);
                    table.ForeignKey(
                        name: "f_k_property_notifications_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_property_notifications_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_email_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_text = table.Column<string>(type: "text", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_notification_email_outbox", x => x.id);
                    table.ForeignKey(
                        name: "f_k_notification_email_outbox_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_notification_email_outbox_property_notifications_notificatio~",
                        column: x => x.notification_id,
                        principalTable: "property_notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "property_notification_reads",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_notification_reads", x => new { x.notification_id, x.user_id });
                    table.ForeignKey(
                        name: "f_k_property_notification_reads_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_property_notification_reads_property_notifications_notifica~",
                        column: x => x.notification_id,
                        principalTable: "property_notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_notification_email_outbox_notification_id",
                table: "notification_email_outbox",
                column: "notification_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_notification_email_outbox_property_id",
                table: "notification_email_outbox",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "i_x_notification_email_outbox_sent_at_utc_next_attempt_at_utc",
                table: "notification_email_outbox",
                columns: new[] { "sent_at_utc", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_property_notification_reads_user_id_read_at_utc",
                table: "property_notification_reads",
                columns: new[] { "user_id", "read_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_property_notification_settings_property_id",
                table: "property_notification_settings",
                column: "property_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_property_notifications_booking_id",
                table: "property_notifications",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "i_x_property_notifications_property_id_created_at_utc",
                table: "property_notifications",
                columns: new[] { "property_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_property_notifications_property_id_type_booking_id",
                table: "property_notifications",
                columns: new[] { "property_id", "type", "booking_id" },
                unique: true,
                filter: "\"booking_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_email_outbox");

            migrationBuilder.DropTable(
                name: "property_notification_reads");

            migrationBuilder.DropTable(
                name: "property_notification_settings");

            migrationBuilder.DropTable(
                name: "property_notifications");
        }
    }
}
