using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PublicPropertySiteSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "site_slug",
                table: "properties",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH base_slugs AS (
                    SELECT
                        id,
                        created_at_utc,
                        CASE
                            WHEN code = 'DELONG' THEN 'de-long'
                            ELSE trim(BOTH '-' FROM regexp_replace(lower(replace(code, '_', '-')), '[^a-z0-9]+', '-', 'g'))
                        END AS raw_slug
                    FROM properties
                ),
                safe_slugs AS (
                    SELECT
                        id,
                        created_at_utc,
                        CASE
                            WHEN length(raw_slug) >= 2 THEN left(raw_slug, 100)
                            ELSE 'property-' || left(replace(id::text, '-', ''), 8)
                        END AS base_slug
                    FROM base_slugs
                ),
                ranked AS (
                    SELECT
                        id,
                        base_slug,
                        row_number() OVER (PARTITION BY base_slug ORDER BY created_at_utc, id) AS duplicate_number
                    FROM safe_slugs
                )
                UPDATE properties AS p
                SET site_slug = CASE
                    WHEN r.duplicate_number = 1 THEN r.base_slug
                    ELSE left(r.base_slug, greatest(1, 90 - length(r.duplicate_number::text)))
                         || '-' || r.duplicate_number::text
                         || '-' || left(replace(r.id::text, '-', ''), 8)
                END
                FROM ranked AS r
                WHERE p.id = r.id;
                """);

            migrationBuilder.CreateIndex(
                name: "i_x_properties_site_slug",
                table: "properties",
                column: "site_slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_properties_site_slug",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "site_slug",
                table: "properties");
        }
    }
}
