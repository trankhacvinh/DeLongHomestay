using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomConditionReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_condition_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inspection_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    tags_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_condition_reports", x => x.id);
                    table.ForeignKey(
                        name: "f_k_room_condition_reports_asp_net_users_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_room_condition_reports_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_room_condition_reports_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_condition_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_condition_tags", x => x.id);
                    table.ForeignKey(
                        name: "f_k_room_condition_tags_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_condition_report_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    original_storage_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    large_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    card_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    thumbnail_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_bytes = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_condition_report_images", x => x.id);
                    table.ForeignKey(
                        name: "f_k_room_condition_report_images_room_condition_reports_report_~",
                        column: x => x.report_id,
                        principalTable: "room_condition_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_room_condition_report_images_report_id_sort_order",
                table: "room_condition_report_images",
                columns: new[] { "report_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "i_x_room_condition_reports_property_id_created_at_utc",
                table: "room_condition_reports",
                columns: new[] { "property_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_room_condition_reports_reported_by_user_id",
                table: "room_condition_reports",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_room_condition_reports_room_id_created_at_utc",
                table: "room_condition_reports",
                columns: new[] { "room_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_room_condition_tags_property_id_normalized_name",
                table: "room_condition_tags",
                columns: new[] { "property_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_condition_report_images");

            migrationBuilder.DropTable(
                name: "room_condition_tags");

            migrationBuilder.DropTable(
                name: "room_condition_reports");
        }
    }
}
