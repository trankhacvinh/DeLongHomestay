using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedInventoryAndCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "request_hash",
                table: "expenses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "request_key",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "room_id",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_voided = table.Column<bool>(type: "boolean", nullable: false),
                    voided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_cash_receipts", x => x.id);
                    table.CheckConstraint("ck_cash_receipt_amount", "amount > 0");
                    table.ForeignKey(
                        name: "f_k_cash_receipts_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_cash_receipts_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_inventory_warehouses", x => x.id);
                    table.ForeignKey(
                        name: "f_k_inventory_warehouses_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recipient_property_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_voided = table.Column<bool>(type: "boolean", nullable: false),
                    voided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_inventory_documents", x => x.id);
                    table.CheckConstraint("ck_inventory_document_recipient", "(type = 2 AND recipient_property_id IS NOT NULL) OR (type <> 2 AND recipient_property_id IS NULL)");
                    table.CheckConstraint("ck_inventory_document_type", "type BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "f_k_inventory_documents_inventory_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "inventory_warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_inventory_documents_properties_recipient_property_id",
                        column: x => x.recipient_property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_inventory_units", x => x.id);
                    table.ForeignKey(
                        name: "f_k_inventory_units_inventory_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "inventory_warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reference_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_inventory_items", x => x.id);
                    table.CheckConstraint("ck_inventory_item_price", "reference_price >= 0");
                    table.ForeignKey(
                        name: "f_k_inventory_items_inventory_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "inventory_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_inventory_items_inventory_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "inventory_warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_document_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_inventory_document_lines", x => x.id);
                    table.CheckConstraint("ck_inventory_line_values", "quantity > 0 AND unit_price >= 0");
                    table.ForeignKey(
                        name: "f_k_inventory_document_lines_inventory_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "inventory_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_inventory_document_lines_inventory_items_item_id",
                        column: x => x.item_id,
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_expenses_property_id_request_key",
                table: "expenses",
                columns: new[] { "property_id", "request_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_expenses_room_id",
                table: "expenses",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "i_x_cash_receipts_property_id_occurred_at_utc",
                table: "cash_receipts",
                columns: new[] { "property_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_cash_receipts_property_id_request_key",
                table: "cash_receipts",
                columns: new[] { "property_id", "request_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_cash_receipts_room_id",
                table: "cash_receipts",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_document_lines_document_id_item_id",
                table: "inventory_document_lines",
                columns: new[] { "document_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_document_lines_item_id",
                table: "inventory_document_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_documents_recipient_property_id",
                table: "inventory_documents",
                column: "recipient_property_id");

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_documents_warehouse_id_occurred_at_utc",
                table: "inventory_documents",
                columns: new[] { "warehouse_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_documents_warehouse_id_request_key",
                table: "inventory_documents",
                columns: new[] { "warehouse_id", "request_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_items_unit_id",
                table: "inventory_items",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_items_warehouse_id_code",
                table: "inventory_items",
                columns: new[] { "warehouse_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_units_warehouse_id_normalized_name",
                table: "inventory_units",
                columns: new[] { "warehouse_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_warehouses_code",
                table: "inventory_warehouses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_warehouses_property_id",
                table: "inventory_warehouses",
                column: "property_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_expenses_rooms_room_id",
                table: "expenses",
                column: "room_id",
                principalTable: "rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_expenses_rooms_room_id",
                table: "expenses");

            migrationBuilder.DropTable(
                name: "cash_receipts");

            migrationBuilder.DropTable(
                name: "inventory_document_lines");

            migrationBuilder.DropTable(
                name: "inventory_documents");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropTable(
                name: "inventory_units");

            migrationBuilder.DropTable(
                name: "inventory_warehouses");

            migrationBuilder.DropIndex(
                name: "i_x_expenses_property_id_request_key",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "i_x_expenses_room_id",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "request_hash",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "request_key",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "room_id",
                table: "expenses");
        }
    }
}
