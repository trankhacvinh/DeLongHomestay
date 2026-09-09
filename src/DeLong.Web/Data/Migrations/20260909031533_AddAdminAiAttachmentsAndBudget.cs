using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAiAttachmentsAndBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "budget_warning_percent",
                table: "property_ai_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<decimal>(
                name: "input_cost_per_million_tokens_usd",
                table: "property_ai_profiles",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_budget_usd",
                table: "property_ai_profiles",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "output_cost_per_million_tokens_usd",
                table: "property_ai_profiles",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "estimated_cost_usd",
                table: "ai_usage_records",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ai_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    extracted_text = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ai_attachments", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ai_attachments_ai_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "ai_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_ai_attachments_asp_net_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_ai_profiles_budget_warning_percent",
                table: "property_ai_profiles",
                sql: "budget_warning_percent BETWEEN 1 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_ai_profiles_monthly_budget_usd",
                table: "property_ai_profiles",
                sql: "monthly_budget_usd >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_ai_profiles_token_costs",
                table: "property_ai_profiles",
                sql: "input_cost_per_million_tokens_usd >= 0 AND output_cost_per_million_tokens_usd >= 0");

            migrationBuilder.CreateIndex(
                name: "i_x_ai_attachments_conversation_id_created_at_utc",
                table: "ai_attachments",
                columns: new[] { "conversation_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_ai_attachments_uploaded_by_user_id",
                table: "ai_attachments",
                column: "uploaded_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_attachments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_property_ai_profiles_budget_warning_percent",
                table: "property_ai_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_property_ai_profiles_monthly_budget_usd",
                table: "property_ai_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_property_ai_profiles_token_costs",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "budget_warning_percent",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "input_cost_per_million_tokens_usd",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "monthly_budget_usd",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "output_cost_per_million_tokens_usd",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "estimated_cost_usd",
                table: "ai_usage_records");
        }
    }
}
