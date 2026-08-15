using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyEditorialContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blog_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    excerpt = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    cover_image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_blog_posts", x => x.id);
                    table.ForeignKey(
                        name: "f_k_blog_posts_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "global_editorial_showcases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gallery_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    gallery_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    gallery_property_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    gallery_item_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    gallery_limit = table.Column<int>(type: "integer", nullable: false),
                    gallery_title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    blog_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    blog_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    blog_property_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    blog_post_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    blog_limit = table.Column<int>(type: "integer", nullable: false),
                    blog_title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_global_editorial_showcases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "property_gallery_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    alt_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_gallery_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_property_gallery_items_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_blog_posts_property_id_is_published_published_at_utc",
                table: "blog_posts",
                columns: new[] { "property_id", "is_published", "published_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_blog_posts_property_id_slug",
                table: "blog_posts",
                columns: new[] { "property_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_property_gallery_items_property_id_sort_order",
                table: "property_gallery_items",
                columns: new[] { "property_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blog_posts");

            migrationBuilder.DropTable(
                name: "global_editorial_showcases");

            migrationBuilder.DropTable(
                name: "property_gallery_items");
        }
    }
}
