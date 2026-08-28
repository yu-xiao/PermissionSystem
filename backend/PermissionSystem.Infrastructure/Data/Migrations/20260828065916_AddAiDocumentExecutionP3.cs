using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiDocumentExecutionP3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_document_confirmation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraftVersion = table.Column<int>(type: "int", nullable: false),
                    ConfirmationVersion = table.Column<int>(type: "int", nullable: false),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    HandlerVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_ai_document_confirmation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_document_confirmation_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_confirmation_ai_document_draft_DraftId",
                        column: x => x.DraftId,
                        principalTable: "ai_document_draft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_confirmation_ai_run_RunId",
                        column: x => x.RunId,
                        principalTable: "ai_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_document_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmationVersion = table.Column<int>(type: "int", nullable: false),
                    DraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessIdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BusinessEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BusinessStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OutboxMessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_ai_document_execution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_document_execution_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_execution_ai_document_confirmation_ConfirmationId",
                        column: x => x.ConfirmationId,
                        principalTable: "ai_document_confirmation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_execution_ai_document_draft_DraftId",
                        column: x => x.DraftId,
                        principalTable: "ai_document_draft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_document_execution_ai_run_RunId",
                        column: x => x.RunId,
                        principalTable: "ai_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_confirmation_ActorUserId",
                table: "ai_document_confirmation",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_confirmation_DraftId",
                table: "ai_document_confirmation",
                column: "DraftId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_confirmation_IsDeleted",
                table: "ai_document_confirmation",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_confirmation_RunId",
                table: "ai_document_confirmation",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_confirmation_TenantId",
                table: "ai_document_confirmation",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_confirmation_TenantId_ActorUserId_Status_ExpiresAt",
                table: "ai_document_confirmation",
                columns: new[] { "TenantId", "ActorUserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_confirmation_TenantId_DraftId_DraftVersion",
                table: "ai_document_confirmation",
                columns: new[] { "TenantId", "DraftId", "DraftVersion" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_confirmation_TenantId_RunId_CreatedAt",
                table: "ai_document_confirmation",
                columns: new[] { "TenantId", "RunId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_ActorUserId",
                table: "ai_document_execution",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_ConfirmationId",
                table: "ai_document_execution",
                column: "ConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_DraftId",
                table: "ai_document_execution",
                column: "DraftId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_IsDeleted",
                table: "ai_document_execution",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_RunId",
                table: "ai_document_execution",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_TenantId",
                table: "ai_document_execution",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_TenantId_BusinessEntityId_CreatedAt",
                table: "ai_document_execution",
                columns: new[] { "TenantId", "BusinessEntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_TenantId_BusinessIdempotencyKey",
                table: "ai_document_execution",
                columns: new[] { "TenantId", "BusinessIdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_TenantId_ConfirmationId_ConfirmationVersion",
                table: "ai_document_execution",
                columns: new[] { "TenantId", "ConfirmationId", "ConfirmationVersion" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_TenantId_RunId_CreatedAt",
                table: "ai_document_execution",
                columns: new[] { "TenantId", "RunId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_TenantId_Status_CreatedAt",
                table: "ai_document_execution",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_document_execution_TenantId_TraceId",
                table: "ai_document_execution",
                columns: new[] { "TenantId", "TraceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_document_execution");

            migrationBuilder.DropTable(
                name: "ai_document_confirmation");
        }
    }
}
