using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRunGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadlineAt",
                table: "ai_run",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionLeaseId",
                table: "ai_run",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastHeartbeatAt",
                table: "ai_run",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetryOfRunId",
                table: "ai_run",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_RetryOfRunId",
                table: "ai_run",
                column: "RetryOfRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_RetryOfRunId",
                table: "ai_run",
                columns: new[] { "TenantId", "RetryOfRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_Status_LastHeartbeatAt",
                table: "ai_run",
                columns: new[] { "TenantId", "Status", "LastHeartbeatAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ai_run_ai_run_RetryOfRunId",
                table: "ai_run",
                column: "RetryOfRunId",
                principalTable: "ai_run",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_run_ai_run_RetryOfRunId",
                table: "ai_run");

            migrationBuilder.DropIndex(
                name: "IX_ai_run_RetryOfRunId",
                table: "ai_run");

            migrationBuilder.DropIndex(
                name: "IX_ai_run_TenantId_RetryOfRunId",
                table: "ai_run");

            migrationBuilder.DropIndex(
                name: "IX_ai_run_TenantId_Status_LastHeartbeatAt",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "DeadlineAt",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ExecutionLeaseId",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "RetryOfRunId",
                table: "ai_run");
        }
    }
}
