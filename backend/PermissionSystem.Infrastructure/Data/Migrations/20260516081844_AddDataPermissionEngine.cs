using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataPermissionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Departments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Enabled");

            migrationBuilder.AddColumn<string>(
                name: "TreePath",
                table: "Departments",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RoleDataScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    CustomDepartmentIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleDataScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleDataScopes_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserDataScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    CustomDepartmentIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDataScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDataScopes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId_ParentId",
                table: "Departments",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleDataScopes_IsDeleted",
                table: "RoleDataScopes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RoleDataScopes_RoleId",
                table: "RoleDataScopes",
                column: "RoleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleDataScopes_TenantId",
                table: "RoleDataScopes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleDataScopes_TenantId_RoleId",
                table: "RoleDataScopes",
                columns: new[] { "TenantId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDataScopes_IsDeleted",
                table: "UserDataScopes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_UserDataScopes_TenantId",
                table: "UserDataScopes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDataScopes_TenantId_UserId",
                table: "UserDataScopes",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDataScopes_UserId",
                table: "UserDataScopes",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleDataScopes");

            migrationBuilder.DropTable(
                name: "UserDataScopes");

            migrationBuilder.DropIndex(
                name: "IX_Departments_TenantId_ParentId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "TreePath",
                table: "Departments");
        }
    }
}
