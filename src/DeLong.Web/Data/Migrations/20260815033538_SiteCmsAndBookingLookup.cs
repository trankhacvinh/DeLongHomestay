using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SiteCmsAndBookingLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "home_section",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    variant = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    content_json = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_home_section", x => x.id);
                    table.ForeignKey(
                        name: "f_k_home_section_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "property_site_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tagline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    facebook_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    zalo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    google_maps_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    favicon_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    og_image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    meta_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    meta_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    canonical_base_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    og_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    og_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    google_site_verification = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    robots_index = table.Column<bool>(type: "boolean", nullable: false),
                    custom_css = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    custom_js = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_site_settings", x => x.id);
                    table.ForeignKey(
                        name: "f_k_property_site_settings_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_home_section_property_id_sort_order",
                table: "home_section",
                columns: new[] { "property_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "i_x_property_site_settings_property_id",
                table: "property_site_settings",
                column: "property_id",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO property_site_settings
                    (id, property_id, site_name, tagline, address, phone, meta_title, meta_description, robots_index, created_at_utc, updated_at_utc)
                SELECT
                    '0198a5a0-1000-7000-8000-000000000101'::uuid,
                    p.id,
                    'De Long Homestay',
                    'Long Thành · Đồng Nai',
                    'Hẻm 39 Nguyễn Đình Chiểu, khu Phước Hải, Long Thành, Đồng Nai',
                    '0352291921',
                    'De Long Homestay',
                    'Không gian nghỉ riêng tư tại Long Thành, Đồng Nai với lựa chọn theo khung giờ, qua đêm và lưu trú nhiều ngày.',
                    TRUE,
                    NOW(),
                    NOW()
                FROM properties p
                WHERE p.code = 'DELONG'
                  AND NOT EXISTS (SELECT 1 FROM property_site_settings s WHERE s.property_id = p.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "home_section");

            migrationBuilder.DropTable(
                name: "property_site_settings");
        }
    }
}
