using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPay2SPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pay2_s_payment_intents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    order_info = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    site_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pay_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancel_booking_on_expiry = table.Column<bool>(type: "boolean", nullable: false),
                    transaction_id = table.Column<long>(type: "bigint", nullable: true),
                    pay_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    result_code = table.Column<int>(type: "integer", nullable: true),
                    provider_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    callback_received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_pay2_s_payment_intents", x => x.id);
                    table.ForeignKey(
                        name: "f_k_pay2_s_payment_intents_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_pay2_s_payment_intents_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_pay2_s_payment_intents_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_pay2_s_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sandbox = table.Column<bool>(type: "boolean", nullable: false),
                    partner_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    partner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    access_key_protected = table.Column<string>(type: "text", nullable: false),
                    secret_key_protected = table.Column<string>(type: "text", nullable: false),
                    bank_account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bank_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    api_endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    callback_base_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    hold_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_pay2_s_settings", x => x.id);
                    table.CheckConstraint("ck_property_pay2s_settings_hold_minutes", "hold_minutes BETWEEN 1 AND 60");
                    table.ForeignKey(
                        name: "f_k_property_pay2_s_settings_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_booking_id",
                table: "pay2_s_payment_intents",
                column: "booking_id",
                unique: true,
                filter: "\"status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_order_id",
                table: "pay2_s_payment_intents",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_payment_id",
                table: "pay2_s_payment_intents",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_property_id",
                table: "pay2_s_payment_intents",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_request_id",
                table: "pay2_s_payment_intents",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_status_expires_at_utc",
                table: "pay2_s_payment_intents",
                columns: new[] { "status", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_transaction_id",
                table: "pay2_s_payment_intents",
                column: "transaction_id",
                unique: true,
                filter: "\"transaction_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_property_pay2_s_settings_property_id",
                table: "property_pay2_s_settings",
                column: "property_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pay2_s_payment_intents");

            migrationBuilder.DropTable(
                name: "property_pay2_s_settings");
        }
    }
}
