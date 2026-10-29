using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryRecipientNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_document_recipient",
                table: "inventory_documents");

            migrationBuilder.AddColumn<string>(
                name: "recipient_name",
                table: "inventory_documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_document_recipient",
                table: "inventory_documents",
                sql: "(type = 2 AND (recipient_property_id IS NOT NULL OR length(btrim(coalesce(recipient_name, ''))) > 0)) OR (type <> 2 AND recipient_property_id IS NULL AND recipient_name IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_document_recipient",
                table: "inventory_documents");

            migrationBuilder.DropColumn(
                name: "recipient_name",
                table: "inventory_documents");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_document_recipient",
                table: "inventory_documents",
                sql: "(type = 2 AND recipient_property_id IS NOT NULL) OR (type <> 2 AND recipient_property_id IS NULL)");
        }
    }
}
