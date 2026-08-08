using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EA016ReportExecutionIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "ReportExecutionLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuccess",
                table: "ReportExecutionLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DatasetKey",
                table: "ReportDefinitions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE ReportExecutionLogs SET IsSuccess = 1 WHERE IsSuccess = 0;");

            migrationBuilder.Sql(
                "UPDATE ReportDefinitions SET DatasetKey = CASE ReportCode " +
                "WHEN 'SystemUserList' THEN 'system-users' " +
                "WHEN 'SystemLoginLogs' THEN 'system-login-logs' " +
                "WHEN 'SystemOperationLogs' THEN 'system-operation-logs' " +
                "ELSE DatasetKey END " +
                "WHERE ReportCode IN ('SystemUserList', 'SystemLoginLogs', 'SystemOperationLogs');");

            migrationBuilder.Sql(
                "UPDATE ReportDefinitions SET SqlText = NULL WHERE DataSourceType = 'Sql';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "ReportExecutionLogs");

            migrationBuilder.DropColumn(
                name: "IsSuccess",
                table: "ReportExecutionLogs");

            migrationBuilder.DropColumn(
                name: "DatasetKey",
                table: "ReportDefinitions");
        }
    }
}
