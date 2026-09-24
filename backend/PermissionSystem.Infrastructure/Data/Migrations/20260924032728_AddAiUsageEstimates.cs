using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedInputTokens",
                table: "ai_usage_log",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedOutputTokens",
                table: "ai_usage_log",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedInputTokens",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "EstimatedOutputTokens",
                table: "ai_usage_log");
        }
    }
}
