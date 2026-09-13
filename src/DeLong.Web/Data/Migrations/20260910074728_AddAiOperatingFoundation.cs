using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiOperatingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "admin_budget_reserve_percent",
                table: "property_ai_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "is_public_ai_enabled",
                table: "property_ai_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "public_requests_per_day",
                table: "property_ai_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "public_requests_per_minute",
                table: "property_ai_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<string>(
                name: "audience",
                table: "ai_usage_records",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Admin");

            migrationBuilder.AddColumn<int>(
                name: "cached_input_tokens",
                table: "ai_usage_records",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_cache_hit",
                table: "ai_usage_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ai_conversation_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    last_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    covered_message_count = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ai_conversation_summaries", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ai_conversation_summaries_ai_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "ai_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_response_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cache_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    intent = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    invalidation_tag = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    data_version = table.Column<long>(type: "bigint", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_hit_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    hit_count = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ai_response_cache", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ai_response_cache_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_tool_execution_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tool_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ai_tool_execution_logs", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ai_tool_execution_logs_ai_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "ai_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_ai_tool_execution_logs_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_ai_tool_execution_logs_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_ai_knowledge_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    content_json = table.Column<string>(type: "jsonb", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    built_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_dirty = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_property_ai_knowledge_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "f_k_property_ai_knowledge_snapshots_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_ai_profiles_admin_budget_reserve_percent",
                table: "property_ai_profiles",
                sql: "admin_budget_reserve_percent BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_ai_profiles_public_request_limits",
                table: "property_ai_profiles",
                sql: "public_requests_per_minute BETWEEN 1 AND 1000 AND public_requests_per_day BETWEEN 1 AND 1000000");

            migrationBuilder.CreateIndex(
                name: "i_x_ai_conversation_summaries_conversation_id",
                table: "ai_conversation_summaries",
                column: "conversation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_ai_response_cache_property_id_audience_cache_key",
                table: "ai_response_cache",
                columns: new[] { "property_id", "audience", "cache_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_ai_response_cache_property_id_invalidation_tag_expires_at_u~",
                table: "ai_response_cache",
                columns: new[] { "property_id", "invalidation_tag", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_ai_tool_execution_logs_conversation_id",
                table: "ai_tool_execution_logs",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ai_tool_execution_logs_property_id_created_at_utc",
                table: "ai_tool_execution_logs",
                columns: new[] { "property_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_ai_tool_execution_logs_user_id",
                table: "ai_tool_execution_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_property_ai_knowledge_snapshots_property_id",
                table: "property_ai_knowledge_snapshots",
                column: "property_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_conversation_summaries");

            migrationBuilder.DropTable(
                name: "ai_response_cache");

            migrationBuilder.DropTable(
                name: "ai_tool_execution_logs");

            migrationBuilder.DropTable(
                name: "property_ai_knowledge_snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_property_ai_profiles_admin_budget_reserve_percent",
                table: "property_ai_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_property_ai_profiles_public_request_limits",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "admin_budget_reserve_percent",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "is_public_ai_enabled",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "public_requests_per_day",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "public_requests_per_minute",
                table: "property_ai_profiles");

            migrationBuilder.DropColumn(
                name: "audience",
                table: "ai_usage_records");

            migrationBuilder.DropColumn(
                name: "cached_input_tokens",
                table: "ai_usage_records");

            migrationBuilder.DropColumn(
                name: "is_cache_hit",
                table: "ai_usage_records");
        }
    }
}
