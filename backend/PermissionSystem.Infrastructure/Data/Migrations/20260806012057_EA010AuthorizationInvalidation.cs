using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EA010AuthorizationInvalidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.Sql(
                """
                UPDATE sessions
                SET sessions.IsRevoked = 1,
                    sessions.RevokedAt = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
                    sessions.RevokedReason = 'Revoked during EA-010 rollout because the user is inactive.'
                FROM UserSessions AS sessions
                INNER JOIN Users AS users
                    ON users.TenantId = sessions.TenantId
                    AND users.Id = sessions.UserId
                WHERE sessions.IsDeleted = 0
                    AND sessions.IsRevoked = 0
                    AND (users.IsDeleted = 1 OR users.IsEnabled = 0);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId_RoleId_UserId",
                table: "UserRoles",
                columns: new[] { "TenantId", "RoleId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId_PermissionId_RoleId",
                table: "RolePermissions",
                columns: new[] { "TenantId", "PermissionId", "RoleId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_TenantId_RoleId_UserId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_TenantId_PermissionId_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");
        }
    }
}
