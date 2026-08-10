using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHousekeepingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "housekeeping_status",
                table: "rooms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Clean");

            migrationBuilder.AddColumn<DateTime>(
                name: "housekeeping_updated_at_utc",
                table: "rooms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "housekeeping_updated_by_user_id",
                table: "rooms",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "housekeeping_status",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "housekeeping_updated_at_utc",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "housekeeping_updated_by_user_id",
                table: "rooms");
        }
    }
}
