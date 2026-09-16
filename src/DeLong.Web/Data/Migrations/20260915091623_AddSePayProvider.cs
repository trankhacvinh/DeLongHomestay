using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSePayProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_pay2_s_payment_intents_transaction_id",
                table: "pay2_s_payment_intents");

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "pay2_s_payment_intents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pay2S");

            migrationBuilder.AddColumn<string>(
                name: "se_pay_bank_account",
                table: "pay2_s_payment_intents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "se_pay_qr_url",
                table: "pay2_s_payment_intents",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "se_pay_sub_account",
                table: "pay2_s_payment_intents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "se_pay_webhook_key_protected",
                table: "pay2_s_payment_intents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "property_se_pay_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_token_protected = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    bank_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    bank_account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    qr_account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_holder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sub_account = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    memo_prefix = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    webhook_key_protected = table.Column<string>(type: "text", nullable: false),
                    hold_minutes = table.Column<int>(type: "integer", nullable: false),
                    settlement_grace_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_se_pay_settings", x => x.id);
                    table.CheckConstraint("ck_sepay_hold", "hold_minutes BETWEEN 1 AND 60 AND settlement_grace_minutes BETWEEN 0 AND 15");
                    table.ForeignKey(
                        name: "f_k_property_se_pay_settings_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "se_pay_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<long>(type: "bigint", nullable: false),
                    intent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    outcome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    resolution_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_se_pay_transactions", x => x.id);
                    table.ForeignKey(
                        name: "f_k_se_pay_transactions_pay2_s_payment_intents_intent_id",
                        column: x => x.intent_id,
                        principalTable: "pay2_s_payment_intents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_se_pay_transactions_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_provider_transaction_id",
                table: "pay2_s_payment_intents",
                columns: new[] { "provider", "transaction_id" },
                unique: true,
                filter: "\"transaction_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_property_se_pay_settings_property_id",
                table: "property_se_pay_settings",
                column: "property_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_se_pay_transactions_intent_id",
                table: "se_pay_transactions",
                column: "intent_id");

            migrationBuilder.CreateIndex(
                name: "i_x_se_pay_transactions_property_id_created_at_utc",
                table: "se_pay_transactions",
                columns: new[] { "property_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_se_pay_transactions_transaction_id",
                table: "se_pay_transactions",
                column: "transaction_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "property_se_pay_settings");

            migrationBuilder.DropTable(
                name: "se_pay_transactions");

            migrationBuilder.DropIndex(
                name: "i_x_pay2_s_payment_intents_provider_transaction_id",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "se_pay_bank_account",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "se_pay_qr_url",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "se_pay_sub_account",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "se_pay_webhook_key_protected",
                table: "pay2_s_payment_intents");

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_transaction_id",
                table: "pay2_s_payment_intents",
                column: "transaction_id",
                unique: true,
                filter: "\"transaction_id\" IS NOT NULL");
        }
    }
}
