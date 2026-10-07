using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomBookingLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "booking_lock_reason",
                table: "rooms",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "booking_locked_at_utc",
                table: "rooms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "booking_locked_by_user_id",
                table: "rooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_booking_locked",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "i_x_rooms_booking_locked_by_user_id",
                table: "rooms",
                column: "booking_locked_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_rooms_asp_net_users_booking_locked_by_user_id",
                table: "rooms",
                column: "booking_locked_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_rooms_asp_net_users_booking_locked_by_user_id",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "i_x_rooms_booking_locked_by_user_id",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "booking_lock_reason",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "booking_locked_at_utc",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "booking_locked_by_user_id",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "is_booking_locked",
                table: "rooms");
        }
    }
}
