using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ConfigValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ConfigType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GroupCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_IsDeleted",
                table: "SystemConfigs",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_TenantId",
                table: "SystemConfigs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_TenantId_ConfigKey",
                table: "SystemConfigs",
                columns: new[] { "TenantId", "ConfigKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_TenantId_GroupCode_Status",
                table: "SystemConfigs",
                columns: new[] { "TenantId", "GroupCode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemConfigs");
        }
    }
}
