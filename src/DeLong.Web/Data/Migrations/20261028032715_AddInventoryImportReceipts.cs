using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryImportReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_inventory_imports", x => x.id);
                    table.ForeignKey(
                        name: "f_k_inventory_imports_inventory_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "inventory_warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_imports_warehouse_id_request_key",
                table: "inventory_imports",
                columns: new[] { "warehouse_id", "request_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_imports");
        }
    }
}
