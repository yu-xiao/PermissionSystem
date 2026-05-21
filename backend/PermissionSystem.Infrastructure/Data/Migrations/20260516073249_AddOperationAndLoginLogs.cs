using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationAndLoginLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperationLogs_TenantId_OperatedAt",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "HttpMethod",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "OperatedAt",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "Succeeded",
                table: "OperationLogs");

            migrationBuilder.RenameColumn(
                name: "OperatorUserId",
                table: "OperationLogs",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_OperationLogs_OperatorUserId",
                table: "OperationLogs",
                newName: "IX_OperationLogs_UserId");

            migrationBuilder.AddColumn<long>(
                name: "ElapsedMilliseconds",
                table: "OperationLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "OperationLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestBody",
                table: "OperationLogs",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestMethod",
                table: "OperationLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResponseBody",
                table: "OperationLogs",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "OperationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "OperationLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "OperationLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoginLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LoginType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LoginResult = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_TenantId_CreatedAt",
                table: "OperationLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_TraceId",
                table: "OperationLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_IsDeleted",
                table: "LoginLogs",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_TenantId",
                table: "LoginLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_TenantId_CreatedAt",
                table: "LoginLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_TraceId",
                table: "LoginLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_UserId",
                table: "LoginLogs",
                column: "UserId");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginLogs");

            migrationBuilder.DropIndex(
                name: "IX_OperationLogs_TenantId_CreatedAt",
                table: "OperationLogs");

            migrationBuilder.DropIndex(
                name: "IX_OperationLogs_TraceId",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "ElapsedMilliseconds",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "RequestBody",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "RequestMethod",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "ResponseBody",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "OperationLogs");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "OperationLogs",
                newName: "OperatorUserId");

            migrationBuilder.RenameIndex(
                name: "IX_OperationLogs_UserId",
                table: "OperationLogs",
                newName: "IX_OperationLogs_OperatorUserId");

            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                table: "OperationLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "OperationLogs",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OperatedAt",
                table: "OperationLogs",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<bool>(
                name: "Succeeded",
                table: "OperationLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_TenantId_OperatedAt",
                table: "OperationLogs",
                columns: new[] { "TenantId", "OperatedAt" });
        }
    }
}
