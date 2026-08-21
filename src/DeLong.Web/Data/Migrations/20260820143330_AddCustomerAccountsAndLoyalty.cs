using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAccountsAndLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_customer_account",
                table: "asp_net_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "customer_account_links",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_customer_account_links", x => new { x.user_id, x.property_id, x.customer_id });
                    table.ForeignKey(
                        name: "f_k_customer_account_links_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_customer_account_links_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_customer_account_links_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_account_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    authenticator_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    loyalty_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    loyalty_spend_per_point = table.Column<int>(type: "integer", nullable: false, defaultValue: 10000),
                    benefit_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    terms_title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    terms_html = table.Column<string>(type: "text", nullable: false),
                    terms_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_customer_account_settings", x => x.id);
                    table.CheckConstraint("ck_customer_account_settings_spend_per_point", "loyalty_spend_per_point BETWEEN 1 AND 1000000000");
                    table.ForeignKey(
                        name: "f_k_customer_account_settings_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_account_terms_acceptances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_version = table.Column<int>(type: "integer", nullable: false),
                    accepted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_customer_account_terms_acceptances", x => x.id);
                    table.ForeignKey(
                        name: "f_k_customer_account_terms_acceptances_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_customer_account_terms_acceptances_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    points = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_loyalty_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "f_k_loyalty_ledger_entries_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_loyalty_ledger_entries_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_loyalty_ledger_entries_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_customer_account_links_customer_id",
                table: "customer_account_links",
                column: "customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_customer_account_links_property_id",
                table: "customer_account_links",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "i_x_customer_account_links_user_id_property_id",
                table: "customer_account_links",
                columns: new[] { "user_id", "property_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_customer_account_settings_property_id",
                table: "customer_account_settings",
                column: "property_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_customer_account_terms_acceptances_property_id",
                table: "customer_account_terms_acceptances",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "i_x_customer_account_terms_acceptances_user_id_property_id_term~",
                table: "customer_account_terms_acceptances",
                columns: new[] { "user_id", "property_id", "terms_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_loyalty_ledger_entries_booking_id",
                table: "loyalty_ledger_entries",
                column: "booking_id",
                unique: true,
                filter: "\"booking_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_loyalty_ledger_entries_property_id",
                table: "loyalty_ledger_entries",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "i_x_loyalty_ledger_entries_user_id_property_id_created_at_utc",
                table: "loyalty_ledger_entries",
                columns: new[] { "user_id", "property_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_account_links");

            migrationBuilder.DropTable(
                name: "customer_account_settings");

            migrationBuilder.DropTable(
                name: "customer_account_terms_acceptances");

            migrationBuilder.DropTable(
                name: "loyalty_ledger_entries");

            migrationBuilder.DropColumn(
                name: "is_customer_account",
                table: "asp_net_users");
        }
    }
}
