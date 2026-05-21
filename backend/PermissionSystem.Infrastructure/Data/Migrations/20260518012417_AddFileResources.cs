using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BucketName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Md5 = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileResources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_IsDeleted",
                table: "FileResources",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_TenantId",
                table: "FileResources",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_TenantId_BusinessType_BusinessId",
                table: "FileResources",
                columns: new[] { "TenantId", "BusinessType", "BusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_TenantId_CreatedAt",
                table: "FileResources",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_TenantId_Md5",
                table: "FileResources",
                columns: new[] { "TenantId", "Md5" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileResources");
        }
    }
}
