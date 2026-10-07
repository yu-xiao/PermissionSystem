using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Roles_TenantId_Id",
                table: "Roles",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FileResources_TenantId_Id",
                table: "FileResources",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ai_run_TenantId_Id",
                table: "ai_run",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "ai_knowledge_chunk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    StartLine = table.Column<int>(type: "int", nullable: false),
                    EndLine = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_knowledge_chunk", x => x.Id);
                    table.UniqueConstraint("AK_ai_knowledge_chunk_TenantId_DocumentId_VersionId_Id", x => new { x.TenantId, x.DocumentId, x.VersionId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ai_knowledge_run_reference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvocationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_knowledge_run_reference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_knowledge_run_reference_ai_knowledge_chunk_TenantId_DocumentId_VersionId_ChunkId",
                        columns: x => new { x.TenantId, x.DocumentId, x.VersionId, x.ChunkId },
                        principalTable: "ai_knowledge_chunk",
                        principalColumns: new[] { "TenantId", "DocumentId", "VersionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_knowledge_run_reference_ai_run_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalTable: "ai_run",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_knowledge_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    License = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccessVersion = table.Column<int>(type: "int", nullable: false),
                    LastVersionNumber = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_knowledge_document", x => x.Id);
                    table.UniqueConstraint("AK_ai_knowledge_document_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ai_knowledge_document_role",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_knowledge_document_role", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_knowledge_document_role_Roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_knowledge_document_role_ai_knowledge_document_TenantId_DocumentId",
                        columns: x => new { x.TenantId, x.DocumentId },
                        principalTable: "ai_knowledge_document",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_knowledge_document_version",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    FileResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParserVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParseStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_knowledge_document_version", x => x.Id);
                    table.UniqueConstraint("AK_ai_knowledge_document_version_TenantId_DocumentId_Id", x => new { x.TenantId, x.DocumentId, x.Id });
                    table.CheckConstraint("CK_ai_knowledge_version_Validity", "[ValidUntil] > [ValidFrom]");
                    table.ForeignKey(
                        name: "FK_ai_knowledge_document_version_FileResources_TenantId_FileResourceId",
                        columns: x => new { x.TenantId, x.FileResourceId },
                        principalTable: "FileResources",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_knowledge_document_version_ai_knowledge_document_TenantId_DocumentId",
                        columns: x => new { x.TenantId, x.DocumentId },
                        principalTable: "ai_knowledge_document",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_chunk_IsDeleted",
                table: "ai_knowledge_chunk",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_chunk_TenantId",
                table: "ai_knowledge_chunk",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_chunk_TenantId_VersionId_Sequence",
                table: "ai_knowledge_chunk",
                columns: new[] { "TenantId", "VersionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_IsDeleted",
                table: "ai_knowledge_document",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_TenantId",
                table: "ai_knowledge_document",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_TenantId_Id_CurrentVersionId",
                table: "ai_knowledge_document",
                columns: new[] { "TenantId", "Id", "CurrentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_TenantId_IsDeleted_CreatedAt",
                table: "ai_knowledge_document",
                columns: new[] { "TenantId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_role_IsDeleted",
                table: "ai_knowledge_document_role",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_role_TenantId",
                table: "ai_knowledge_document_role",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_role_TenantId_DocumentId_RoleId",
                table: "ai_knowledge_document_role",
                columns: new[] { "TenantId", "DocumentId", "RoleId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_role_TenantId_RoleId",
                table: "ai_knowledge_document_role",
                columns: new[] { "TenantId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_version_IsDeleted",
                table: "ai_knowledge_document_version",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_version_TenantId",
                table: "ai_knowledge_document_version",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_version_TenantId_DocumentId_ContentHash",
                table: "ai_knowledge_document_version",
                columns: new[] { "TenantId", "DocumentId", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_version_TenantId_DocumentId_VersionNumber",
                table: "ai_knowledge_document_version",
                columns: new[] { "TenantId", "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_document_version_TenantId_FileResourceId",
                table: "ai_knowledge_document_version",
                columns: new[] { "TenantId", "FileResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_run_reference_IsDeleted",
                table: "ai_knowledge_run_reference",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_run_reference_TenantId",
                table: "ai_knowledge_run_reference",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_run_reference_TenantId_DocumentId_RunId",
                table: "ai_knowledge_run_reference",
                columns: new[] { "TenantId", "DocumentId", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_run_reference_TenantId_DocumentId_VersionId_ChunkId",
                table: "ai_knowledge_run_reference",
                columns: new[] { "TenantId", "DocumentId", "VersionId", "ChunkId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_run_reference_TenantId_RunId_InvocationId_ChunkId",
                table: "ai_knowledge_run_reference",
                columns: new[] { "TenantId", "RunId", "InvocationId", "ChunkId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_knowledge_chunk_ai_knowledge_document_version_TenantId_DocumentId_VersionId",
                table: "ai_knowledge_chunk",
                columns: new[] { "TenantId", "DocumentId", "VersionId" },
                principalTable: "ai_knowledge_document_version",
                principalColumns: new[] { "TenantId", "DocumentId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_knowledge_document_ai_knowledge_document_version_TenantId_Id_CurrentVersionId",
                table: "ai_knowledge_document",
                columns: new[] { "TenantId", "Id", "CurrentVersionId" },
                principalTable: "ai_knowledge_document_version",
                principalColumns: new[] { "TenantId", "DocumentId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_knowledge_document_ai_knowledge_document_version_TenantId_Id_CurrentVersionId",
                table: "ai_knowledge_document");

            migrationBuilder.DropTable(
                name: "ai_knowledge_document_role");

            migrationBuilder.DropTable(
                name: "ai_knowledge_run_reference");

            migrationBuilder.DropTable(
                name: "ai_knowledge_chunk");

            migrationBuilder.DropTable(
                name: "ai_knowledge_document_version");

            migrationBuilder.DropTable(
                name: "ai_knowledge_document");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Roles_TenantId_Id",
                table: "Roles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FileResources_TenantId_Id",
                table: "FileResources");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ai_run_TenantId_Id",
                table: "ai_run");
        }
    }
}
