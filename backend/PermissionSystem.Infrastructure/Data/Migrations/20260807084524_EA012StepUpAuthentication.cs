using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EA012StepUpAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SensitiveOperationVerifications_TenantId_UserId_OperationCode_ExpiresAt",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropIndex(
                name: "IX_SensitiveOperationVerifications_TenantId_VerifyCode",
                table: "SensitiveOperationVerifications");

            migrationBuilder.Sql("DELETE FROM [SensitiveOperationVerifications];");

            migrationBuilder.DropColumn(
                name: "VerifyCode",
                table: "SensitiveOperationVerifications");

            migrationBuilder.AddColumn<string>(
                name: "VerificationMethod",
                table: "SensitiveOperationVerifications",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Password");

            migrationBuilder.AddColumn<int>(
                name: "FailedAttemptCount",
                table: "SensitiveOperationVerifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedAt",
                table: "SensitiveOperationVerifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SensitiveOperationVerifications",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "SensitiveOperationVerifications",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TicketExpiresAt",
                table: "SensitiveOperationVerifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketHash",
                table: "SensitiveOperationVerifications",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAt",
                table: "SensitiveOperationVerifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveOperationVerifications_TenantId_TicketHash",
                table: "SensitiveOperationVerifications",
                columns: new[] { "TenantId", "TicketHash" },
                unique: true,
                filter: "[TicketHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveOperationVerifications_TenantId_UserId_SessionId_OperationCode_ExpiresAt",
                table: "SensitiveOperationVerifications",
                columns: new[] { "TenantId", "UserId", "SessionId", "OperationCode", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SensitiveOperationVerifications_TenantId_TicketHash",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropIndex(
                name: "IX_SensitiveOperationVerifications_TenantId_UserId_SessionId_OperationCode_ExpiresAt",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropColumn(
                name: "FailedAttemptCount",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropColumn(
                name: "TicketExpiresAt",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropColumn(
                name: "TicketHash",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "SensitiveOperationVerifications");

            migrationBuilder.DropColumn(
                name: "VerificationMethod",
                table: "SensitiveOperationVerifications");

            migrationBuilder.AddColumn<string>(
                name: "VerifyCode",
                table: "SensitiveOperationVerifications",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveOperationVerifications_TenantId_UserId_OperationCode_ExpiresAt",
                table: "SensitiveOperationVerifications",
                columns: new[] { "TenantId", "UserId", "OperationCode", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveOperationVerifications_TenantId_VerifyCode",
                table: "SensitiveOperationVerifications",
                columns: new[] { "TenantId", "VerifyCode" });
        }
    }
}
