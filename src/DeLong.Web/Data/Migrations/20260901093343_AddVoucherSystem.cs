using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "voucher_email_body_template",
                table: "property_notification_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voucher_email_subject_template",
                table: "property_notification_settings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    applies_to = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_usage_limit = table.Column<int>(type: "integer", nullable: true),
                    per_customer_usage_limit = table.Column<int>(type: "integer", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_vouchers", x => x.id);
                    table.CheckConstraint("ck_vouchers_applicability", "applies_to <> 'None'");
                    table.CheckConstraint("ck_vouchers_discount_percent", "discount_percent > 0 AND discount_percent <= 100");
                    table.CheckConstraint("ck_vouchers_per_customer_usage_limit", "per_customer_usage_limit IS NULL OR per_customer_usage_limit > 0");
                    table.CheckConstraint("ck_vouchers_total_usage_limit", "total_usage_limit IS NULL OR total_usage_limit > 0");
                    table.CheckConstraint("ck_vouchers_validity", "ends_at_utc > starts_at_utc");
                    table.ForeignKey(
                        name: "f_k_vouchers_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_vouchers_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_email_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_text = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_voucher_email_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "f_k_voucher_email_deliveries_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_voucher_email_deliveries_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_voucher_email_deliveries_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_redemptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voucher_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    eligible_room_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    booking_scope = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reserved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    redeemed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    released_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    restored_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    restored_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_voucher_redemptions", x => x.id);
                    table.CheckConstraint("ck_voucher_redemptions_amounts", "eligible_room_amount >= 0 AND discount_amount >= 0 AND discount_amount <= eligible_room_amount");
                    table.ForeignKey(
                        name: "f_k_voucher_redemptions_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_voucher_redemptions_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_voucher_redemptions_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_voucher_redemptions_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_email_deliveries_customer_id",
                table: "voucher_email_deliveries",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_email_deliveries_property_id_voucher_id_created_at_~",
                table: "voucher_email_deliveries",
                columns: new[] { "property_id", "voucher_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_email_deliveries_sent_at_utc_next_attempt_at_utc",
                table: "voucher_email_deliveries",
                columns: new[] { "sent_at_utc", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_email_deliveries_voucher_id",
                table: "voucher_email_deliveries",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_redemptions_booking_id",
                table: "voucher_redemptions",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_redemptions_customer_id",
                table: "voucher_redemptions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_redemptions_property_id",
                table: "voucher_redemptions",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_redemptions_voucher_id_customer_id_status",
                table: "voucher_redemptions",
                columns: new[] { "voucher_id", "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_voucher_redemptions_voucher_id_status",
                table: "voucher_redemptions",
                columns: new[] { "voucher_id", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_vouchers_customer_id",
                table: "vouchers",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "i_x_vouchers_property_id_normalized_code",
                table: "vouchers",
                columns: new[] { "property_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_vouchers_property_id_status_starts_at_utc_ends_at_utc",
                table: "vouchers",
                columns: new[] { "property_id", "status", "starts_at_utc", "ends_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_email_deliveries");

            migrationBuilder.DropTable(
                name: "voucher_redemptions");

            migrationBuilder.DropTable(
                name: "vouchers");

            migrationBuilder.DropColumn(
                name: "voucher_email_body_template",
                table: "property_notification_settings");

            migrationBuilder.DropColumn(
                name: "voucher_email_subject_template",
                table: "property_notification_settings");
        }
    }
}
