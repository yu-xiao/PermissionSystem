using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EA019FileSecurityAclCompensation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "FileResources",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileStatus",
                table: "FileResources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "FileResources",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "FileResources",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "FileResources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScanMessage",
                table: "FileResources",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScanStatus",
                table: "FileResources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "FileResources",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE [FileResources] " +
                "SET [FileStatus] = 1, [ScanStatus] = 1 " +
                "WHERE [FileStatus] = 0 AND [ScanStatus] = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_FileStatus_NextRetryAt_CreatedAt",
                table: "FileResources",
                columns: new[] { "FileStatus", "NextRetryAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_TenantId_FileStatus_ScanStatus_CreatedAt",
                table: "FileResources",
                columns: new[] { "TenantId", "FileStatus", "ScanStatus", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileResources_FileStatus_NextRetryAt_CreatedAt",
                table: "FileResources");

            migrationBuilder.DropIndex(
                name: "IX_FileResources_TenantId_FileStatus_ScanStatus_CreatedAt",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "FileStatus",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "ScanMessage",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "ScanStatus",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "FileResources");
        }
    }
}
