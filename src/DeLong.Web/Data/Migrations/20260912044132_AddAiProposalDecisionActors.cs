using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiProposalDecisionActors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "applied_by_user_id",
                table: "ai_change_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rejected_by_user_id",
                table: "ai_change_proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_ai_change_proposals_applied_by_user_id",
                table: "ai_change_proposals",
                column: "applied_by_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ai_change_proposals_rejected_by_user_id",
                table: "ai_change_proposals",
                column: "rejected_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_ai_change_proposals_asp_net_users_applied_by_user_id",
                table: "ai_change_proposals",
                column: "applied_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "f_k_ai_change_proposals_asp_net_users_rejected_by_user_id",
                table: "ai_change_proposals",
                column: "rejected_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_ai_change_proposals_asp_net_users_applied_by_user_id",
                table: "ai_change_proposals");

            migrationBuilder.DropForeignKey(
                name: "f_k_ai_change_proposals_asp_net_users_rejected_by_user_id",
                table: "ai_change_proposals");

            migrationBuilder.DropIndex(
                name: "i_x_ai_change_proposals_applied_by_user_id",
                table: "ai_change_proposals");

            migrationBuilder.DropIndex(
                name: "i_x_ai_change_proposals_rejected_by_user_id",
                table: "ai_change_proposals");

            migrationBuilder.DropColumn(
                name: "applied_by_user_id",
                table: "ai_change_proposals");

            migrationBuilder.DropColumn(
                name: "rejected_by_user_id",
                table: "ai_change_proposals");
        }
    }
}
