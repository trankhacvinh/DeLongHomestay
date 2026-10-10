using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomBookingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_booking_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    repeat_daily = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_booking_blocks", x => x.id);
                    table.CheckConstraint("ck_room_booking_blocks_interval", "end_utc > start_utc");
                    table.ForeignKey(
                        name: "f_k_room_booking_blocks_asp_net_users_cancelled_by_user_id",
                        column: x => x.cancelled_by_user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_room_booking_blocks_asp_net_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_room_booking_blocks_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_room_booking_blocks_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_room_booking_blocks_cancelled_by_user_id",
                table: "room_booking_blocks",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_room_booking_blocks_created_by_user_id",
                table: "room_booking_blocks",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_room_booking_blocks_property_id_batch_id",
                table: "room_booking_blocks",
                columns: new[] { "property_id", "batch_id" });

            migrationBuilder.CreateIndex(
                name: "i_x_room_booking_blocks_property_id_room_id_start_utc_end_utc",
                table: "room_booking_blocks",
                columns: new[] { "property_id", "room_id", "start_utc", "end_utc" },
                filter: "cancelled_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_room_booking_blocks_room_id",
                table: "room_booking_blocks",
                column: "room_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_booking_blocks");
        }
    }
}
