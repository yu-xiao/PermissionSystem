using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiDocumentDraftP2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_document_draft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInvocationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HandlerVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DraftVersion = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastValidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_ai_document_draft", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_document_draft_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_draft_ai_conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ai_conversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_draft_ai_run_RunId",
                        column: x => x.RunId,
                        principalTable: "ai_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_document_draft_validation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraftVersion = table.Column<int>(type: "int", nullable: false),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    ErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.PrimaryKey("PK_ai_document_draft_validation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_document_draft_validation_ai_document_draft_DraftId",
                        column: x => x.DraftId,
                        principalTable: "ai_document_draft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_ActorUserId",
                table: "ai_document_draft",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_ConversationId",
                table: "ai_document_draft",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_IsDeleted",
                table: "ai_document_draft",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_RunId",
                table: "ai_document_draft",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_TenantId",
                table: "ai_document_draft",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_TenantId_ActorUserId_Status_CreatedAt",
                table: "ai_document_draft",
                columns: new[] { "TenantId", "ActorUserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_TenantId_ConversationId_CreatedAt",
                table: "ai_document_draft",
                columns: new[] { "TenantId", "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_TenantId_RunId_CreatedAt",
                table: "ai_document_draft",
                columns: new[] { "TenantId", "RunId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_TenantId_SourceInvocationId",
                table: "ai_document_draft",
                columns: new[] { "TenantId", "SourceInvocationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_TenantId_Status_ExpiresAt",
                table: "ai_document_draft",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_validation_DraftId",
                table: "ai_document_draft_validation",
                column: "DraftId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_validation_IsDeleted",
                table: "ai_document_draft_validation",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_validation_TenantId",
                table: "ai_document_draft_validation",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_validation_TenantId_DraftId_DraftVersion",
                table: "ai_document_draft_validation",
                columns: new[] { "TenantId", "DraftId", "DraftVersion" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_draft_validation_TenantId_IsValid_ValidatedAt",
                table: "ai_document_draft_validation",
                columns: new[] { "TenantId", "IsValid", "ValidatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_document_draft_validation");

            migrationBuilder.DropTable(
                name: "ai_document_draft");
        }
    }
}
