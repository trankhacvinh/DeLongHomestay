using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPay2SSettlementGrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_pay2_s_payment_intents_status_expires_at_utc",
                table: "pay2_s_payment_intents");

            migrationBuilder.AddColumn<int>(
                name: "settlement_grace_minutes",
                table: "property_pay2_s_settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_callback_attempt_at_utc",
                table: "pay2_s_payment_intents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_callback_error_code",
                table: "pay2_s_payment_intents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "late_payment_resolution",
                table: "pay2_s_payment_intents",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "late_payment_resolution_note",
                table: "pay2_s_payment_intents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "late_payment_resolved_at_utc",
                table: "pay2_s_payment_intents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "late_payment_resolved_by_user_id",
                table: "pay2_s_payment_intents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "release_at_utc",
                table: "pay2_s_payment_intents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE pay2_s_payment_intents
                SET release_at_utc = expires_at_utc + INTERVAL '3 minutes'
                WHERE release_at_utc IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "release_at_utc",
                table: "pay2_s_payment_intents",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_pay2s_settings_settlement_grace",
                table: "property_pay2_s_settings",
                sql: "settlement_grace_minutes BETWEEN 0 AND 15");

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_status_release_at_utc",
                table: "pay2_s_payment_intents",
                columns: new[] { "status", "release_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_property_pay2s_settings_settlement_grace",
                table: "property_pay2_s_settings");

            migrationBuilder.DropIndex(
                name: "i_x_pay2_s_payment_intents_status_release_at_utc",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "settlement_grace_minutes",
                table: "property_pay2_s_settings");

            migrationBuilder.DropColumn(
                name: "last_callback_attempt_at_utc",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "last_callback_error_code",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "late_payment_resolution",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "late_payment_resolution_note",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "late_payment_resolved_at_utc",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "late_payment_resolved_by_user_id",
                table: "pay2_s_payment_intents");

            migrationBuilder.DropColumn(
                name: "release_at_utc",
                table: "pay2_s_payment_intents");

            migrationBuilder.CreateIndex(
                name: "i_x_pay2_s_payment_intents_status_expires_at_utc",
                table: "pay2_s_payment_intents",
                columns: new[] { "status", "expires_at_utc" });
        }
    }
}
