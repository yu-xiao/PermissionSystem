using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledTaskExecutionLogTraceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "ScheduledTaskExecutionLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTaskExecutionLogs_TraceId",
                table: "ScheduledTaskExecutionLogs",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduledTaskExecutionLogs_TraceId",
                table: "ScheduledTaskExecutionLogs");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "ScheduledTaskExecutionLogs");
        }
    }
}
