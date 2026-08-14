using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoomContentV21 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "focal_x",
                table: "room_images",
                type: "double precision",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.AddColumn<double>(
                name: "focal_y",
                table: "room_images",
                type: "double precision",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.CreateTable(
                name: "amenity_presets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_amenity_presets", x => x.id);
                    table.ForeignKey(
                        name: "f_k_amenity_presets_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "amenity_preset_items",
                columns: table => new
                {
                    amenity_preset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amenity_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_amenity_preset_items", x => new { x.amenity_preset_id, x.amenity_id });
                    table.ForeignKey(
                        name: "f_k_amenity_preset_items_amenities_amenity_id",
                        column: x => x.amenity_id,
                        principalTable: "amenities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_amenity_preset_items_amenity_presets_amenity_preset_id",
                        column: x => x.amenity_preset_id,
                        principalTable: "amenity_presets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_amenity_preset_items_amenity_id",
                table: "amenity_preset_items",
                column: "amenity_id");

            migrationBuilder.CreateIndex(
                name: "i_x_amenity_presets_property_id_normalized_name",
                table: "amenity_presets",
                columns: new[] { "property_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "amenity_preset_items");

            migrationBuilder.DropTable(
                name: "amenity_presets");

            migrationBuilder.DropColumn(
                name: "focal_x",
                table: "room_images");

            migrationBuilder.DropColumn(
                name: "focal_y",
                table: "room_images");
        }
    }
}
