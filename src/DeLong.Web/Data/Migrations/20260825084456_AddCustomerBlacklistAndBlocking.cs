using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerBlacklistAndBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "blacklist_reason",
                table: "customers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_blacklisted",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_blocked",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_customers_blacklist_state",
                table: "customers",
                sql: "NOT is_blocked OR (is_blacklisted AND blacklist_reason IS NOT NULL AND length(btrim(blacklist_reason)) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_customers_blacklist_state",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "blacklist_reason",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "is_blacklisted",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "is_blocked",
                table: "customers");
        }
    }
}
