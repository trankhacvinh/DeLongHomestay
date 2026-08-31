using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingEmailAndTelegramNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "guest_cancellation_email_body_template",
                table: "property_notification_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "guest_cancellation_email_enabled",
                table: "property_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "guest_cancellation_email_subject_template",
                table: "property_notification_settings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guest_check_in_email_body_template",
                table: "property_notification_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "guest_check_in_email_enabled",
                table: "property_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "guest_check_in_email_subject_template",
                table: "property_notification_settings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internal_booking_email_body_template",
                table: "property_notification_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internal_booking_email_subject_template",
                table: "property_notification_settings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_telegram_error",
                table: "property_notification_settings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_telegram_error_at_utc",
                table: "property_notification_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_telegram_sent_at_utc",
                table: "property_notification_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "telegram_booking_enabled",
                table: "property_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "telegram_bot_token_protected",
                table: "property_notification_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_chat_ids",
                table: "property_notification_settings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "body_html",
                table: "notification_email_outbox",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_guest_guide_emails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    trigger = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    template_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_text = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_booking_guest_guide_emails", x => x.id);
                    table.ForeignKey(
                        name: "f_k_booking_guest_guide_emails_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_booking_guest_guide_emails_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_telegram_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_ids = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    message_text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_notification_telegram_outbox", x => x.id);
                    table.ForeignKey(
                        name: "f_k_notification_telegram_outbox_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_notification_telegram_outbox_property_notifications_notifica~",
                        column: x => x.notification_id,
                        principalTable: "property_notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_booking_guest_guide_emails_booking_id",
                table: "booking_guest_guide_emails",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "i_x_booking_guest_guide_emails_property_id_booking_id_created_a~",
                table: "booking_guest_guide_emails",
                columns: new[] { "property_id", "booking_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_booking_guest_guide_emails_sent_at_utc_next_attempt_at_utc",
                table: "booking_guest_guide_emails",
                columns: new[] { "sent_at_utc", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_notification_telegram_outbox_notification_id",
                table: "notification_telegram_outbox",
                column: "notification_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_notification_telegram_outbox_property_id",
                table: "notification_telegram_outbox",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "i_x_notification_telegram_outbox_sent_at_utc_next_attempt_at_utc",
                table: "notification_telegram_outbox",
                columns: new[] { "sent_at_utc", "next_attempt_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_guest_guide_emails");

            migrationBuilder.DropTable(
                name: "notification_telegram_outbox");

            migrationBuilder.DropColumn(
                name: "guest_cancellation_email_body_template",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "guest_cancellation_email_enabled",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "guest_cancellation_email_subject_template",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "guest_check_in_email_body_template",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "guest_check_in_email_enabled",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "guest_check_in_email_subject_template",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "internal_booking_email_body_template",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "internal_booking_email_subject_template",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "last_telegram_error",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "last_telegram_error_at_utc",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "last_telegram_sent_at_utc",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "telegram_booking_enabled",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "telegram_bot_token_protected",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "telegram_chat_ids",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "body_html",
                table: "notification_email_outbox");
        }
    }
}
