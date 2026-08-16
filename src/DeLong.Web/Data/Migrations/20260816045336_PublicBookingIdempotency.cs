using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PublicBookingIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "public_request_key",
                table: "bookings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_bookings_property_id_public_request_key",
                table: "bookings",
                columns: new[] { "property_id", "public_request_key" },
                unique: true,
                filter: "\"public_request_key\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_bookings_property_id_public_request_key",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "public_request_key",
                table: "bookings");
        }
    }
}
