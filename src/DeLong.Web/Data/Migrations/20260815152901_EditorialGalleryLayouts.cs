using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class EditorialGalleryLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gallery_layout",
                table: "property_site_settings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "mosaic");

            migrationBuilder.AddColumn<string>(
                name: "gallery_layout",
                table: "global_editorial_showcases",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "mosaic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gallery_layout",
                table: "property_site_settings");

            migrationBuilder.DropColumn(
                name: "gallery_layout",
                table: "global_editorial_showcases");
        }
    }
}
