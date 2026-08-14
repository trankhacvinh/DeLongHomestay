using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoomContentV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description_html",
                table: "rooms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "short_description",
                table: "rooms",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "rooms",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            // Preserve the current public catalog after introducing publication state.
            // Existing room codes are unique per property, so their lower-case form is a safe
            // deterministic initial slug. New rooms remain unpublished until content is ready.
            migrationBuilder.Sql("UPDATE rooms SET slug = lower(code), is_published = TRUE WHERE is_active = TRUE;");
            migrationBuilder.Sql("UPDATE rooms SET slug = lower(code) WHERE slug IS NULL;");

            migrationBuilder.CreateTable(
                name: "amenities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_amenities", x => x.id);
                    table.ForeignKey(
                        name: "f_k_amenities_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_highlights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_highlights", x => x.id);
                    table.ForeignKey(
                        name: "f_k_room_highlights_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    original_storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    large_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    card_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    thumbnail_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_bytes = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    alt_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_cover = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_images", x => x.id);
                    table.ForeignKey(
                        name: "f_k_room_images_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_tags", x => x.id);
                    table.ForeignKey(
                        name: "f_k_room_tags_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_amenities",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amenity_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_amenities", x => new { x.room_id, x.amenity_id });
                    table.ForeignKey(
                        name: "f_k_room_amenities_amenities_amenity_id",
                        column: x => x.amenity_id,
                        principalTable: "amenities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_room_amenities_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_tag_assignments",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_room_tag_assignments", x => new { x.room_id, x.room_tag_id });
                    table.ForeignKey(
                        name: "f_k_room_tag_assignments_room_tags_room_tag_id",
                        column: x => x.room_tag_id,
                        principalTable: "room_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_room_tag_assignments_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_rooms_property_id_slug",
                table: "rooms",
                columns: new[] { "property_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_amenities_property_id_normalized_name",
                table: "amenities",
                columns: new[] { "property_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_room_amenities_amenity_id",
                table: "room_amenities",
                column: "amenity_id");

            migrationBuilder.CreateIndex(
                name: "i_x_room_highlights_room_id_sort_order",
                table: "room_highlights",
                columns: new[] { "room_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "i_x_room_images_room_id_sort_order",
                table: "room_images",
                columns: new[] { "room_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "i_x_room_tag_assignments_room_tag_id",
                table: "room_tag_assignments",
                column: "room_tag_id");

            migrationBuilder.CreateIndex(
                name: "i_x_room_tags_property_id_normalized_name",
                table: "room_tags",
                columns: new[] { "property_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_amenities");

            migrationBuilder.DropTable(
                name: "room_highlights");

            migrationBuilder.DropTable(
                name: "room_images");

            migrationBuilder.DropTable(
                name: "room_tag_assignments");

            migrationBuilder.DropTable(
                name: "amenities");

            migrationBuilder.DropTable(
                name: "room_tags");

            migrationBuilder.DropIndex(
                name: "i_x_rooms_property_id_slug",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "description_html",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "is_published",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "short_description",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "rooms");
        }
    }
}
