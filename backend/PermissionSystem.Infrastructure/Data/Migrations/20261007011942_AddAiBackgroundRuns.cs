using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiBackgroundRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ai_run WHERE IsDeleted = 0 AND Status IN ('Pending', 'Running')
                    GROUP BY TenantId, ConversationId HAVING COUNT(*) > 1)
                    THROW 51000, 'AIC-008 requires review of duplicate active runs before migration.', 1;
                """);
            migrationBuilder.AddColumn<Guid>(
                name: "ActorSecurityStamp",
                table: "ai_run",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorSessionId",
                table: "ai_run",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionMode",
                table: "ai_run",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProgressVersion",
                table: "ai_run",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QueueDeadlineAt",
                table: "ai_run",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "ai_run",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionHash",
                table: "ai_run",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_ExecutionMode_Status_CreatedAt",
                table: "ai_run",
                columns: new[] { "ExecutionMode", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_ActorUserId_ConversationId_SubmissionHash",
                table: "ai_run",
                columns: new[] { "TenantId", "ActorUserId", "ConversationId", "SubmissionHash" },
                unique: true,
                filter: "[SubmissionHash] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_ConversationId",
                table: "ai_run",
                columns: new[] { "TenantId", "ConversationId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN ('Pending', 'Running')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_run_ExecutionMode_Status_CreatedAt",
                table: "ai_run");

            migrationBuilder.DropIndex(
                name: "IX_ai_run_TenantId_ActorUserId_ConversationId_SubmissionHash",
                table: "ai_run");

            migrationBuilder.DropIndex(
                name: "IX_ai_run_TenantId_ConversationId",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ActorSecurityStamp",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ActorSessionId",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ExecutionMode",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ProgressVersion",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "QueueDeadlineAt",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "SubmissionHash",
                table: "ai_run");
        }
    }
}
