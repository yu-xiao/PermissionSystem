using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCenterP1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_conversation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AgentVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetentionUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.PrimaryKey("PK_ai_conversation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_conversation_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_provider_config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ChatCompletionsPath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    Temperature = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    MaxTokens = table.Column<int>(type: "int", nullable: true),
                    AllowInsecureHttp = table.Column<bool>(type: "bit", nullable: false),
                    AllowPrivateNetwork = table.Column<bool>(type: "bit", nullable: false),
                    AllowedHostsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DataResidency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ai_provider_config", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_message",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentClassification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContentDigest = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TokenCount = table.Column<int>(type: "int", nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ModelGenerated = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ai_message", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_message_ai_conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ai_conversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_run",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AgentVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancellationRequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_ai_run", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_run_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_run_ai_conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ai_conversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_run_ai_message_RequestMessageId",
                        column: x => x.RequestMessageId,
                        principalTable: "ai_message",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_run_ai_message_ResponseMessageId",
                        column: x => x.ResponseMessageId,
                        principalTable: "ai_message",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_run_ai_provider_config_ProviderConfigId",
                        column: x => x.ProviderConfigId,
                        principalTable: "ai_provider_config",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_tool_invocation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvocationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ToolCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToolVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InputDigest = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OutputDigest = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DatasetCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DatasetVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowCount = table.Column<int>(type: "int", nullable: true),
                    IsTruncated = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CitationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ai_tool_invocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_tool_invocation_ai_run_RunId",
                        column: x => x.RunId,
                        principalTable: "ai_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    FinishReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_ai_usage_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_usage_log_ai_provider_config_ProviderConfigId",
                        column: x => x.ProviderConfigId,
                        principalTable: "ai_provider_config",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_usage_log_ai_run_RunId",
                        column: x => x.RunId,
                        principalTable: "ai_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_IsDeleted",
                table: "ai_conversation",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_TenantId",
                table: "ai_conversation",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_TenantId_RetentionUntil",
                table: "ai_conversation",
                columns: new[] { "TenantId", "RetentionUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_TenantId_Status_LastMessageAt",
                table: "ai_conversation",
                columns: new[] { "TenantId", "Status", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_TenantId_UserId_LastMessageAt",
                table: "ai_conversation",
                columns: new[] { "TenantId", "UserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_UserId",
                table: "ai_conversation",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_message_ConversationId",
                table: "ai_message",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_message_IsDeleted",
                table: "ai_message",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_message_TenantId",
                table: "ai_message",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_message_TenantId_ConversationId_Sequence",
                table: "ai_message",
                columns: new[] { "TenantId", "ConversationId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_config_IsDeleted",
                table: "ai_provider_config",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_config_TenantId",
                table: "ai_provider_config",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_config_TenantId_IsDefault",
                table: "ai_provider_config",
                columns: new[] { "TenantId", "IsDefault" },
                unique: true,
                filter: "[IsDefault] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_config_TenantId_IsEnabled_IsDefault",
                table: "ai_provider_config",
                columns: new[] { "TenantId", "IsEnabled", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_config_TenantId_ProviderCode",
                table: "ai_provider_config",
                columns: new[] { "TenantId", "ProviderCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_ActorUserId",
                table: "ai_run",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_ConversationId",
                table: "ai_run",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_IsDeleted",
                table: "ai_run",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_ProviderConfigId",
                table: "ai_run",
                column: "ProviderConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_RequestMessageId",
                table: "ai_run",
                column: "RequestMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_ResponseMessageId",
                table: "ai_run",
                column: "ResponseMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId",
                table: "ai_run",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_ActorUserId_CreatedAt",
                table: "ai_run",
                columns: new[] { "TenantId", "ActorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_ConversationId_CreatedAt",
                table: "ai_run",
                columns: new[] { "TenantId", "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_Status_CreatedAt",
                table: "ai_run",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_TraceId",
                table: "ai_run",
                columns: new[] { "TenantId", "TraceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_invocation_IsDeleted",
                table: "ai_tool_invocation",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_invocation_RunId",
                table: "ai_tool_invocation",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_invocation_TenantId",
                table: "ai_tool_invocation",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_invocation_TenantId_InvocationId",
                table: "ai_tool_invocation",
                columns: new[] { "TenantId", "InvocationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_invocation_TenantId_RunId_CreatedAt",
                table: "ai_tool_invocation",
                columns: new[] { "TenantId", "RunId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_invocation_TenantId_Status_CreatedAt",
                table: "ai_tool_invocation",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_invocation_TenantId_ToolCode_CreatedAt",
                table: "ai_tool_invocation",
                columns: new[] { "TenantId", "ToolCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_log_IsDeleted",
                table: "ai_usage_log",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_log_ProviderConfigId",
                table: "ai_usage_log",
                column: "ProviderConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_log_RunId",
                table: "ai_usage_log",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_log_TenantId",
                table: "ai_usage_log",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_log_TenantId_ProviderConfigId_CreatedAt",
                table: "ai_usage_log",
                columns: new[] { "TenantId", "ProviderConfigId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_log_TenantId_RunId_Sequence",
                table: "ai_usage_log",
                columns: new[] { "TenantId", "RunId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_log_TenantId_Status_CreatedAt",
                table: "ai_usage_log",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_tool_invocation");

            migrationBuilder.DropTable(
                name: "ai_usage_log");

            migrationBuilder.DropTable(
                name: "ai_run");

            migrationBuilder.DropTable(
                name: "ai_message");

            migrationBuilder.DropTable(
                name: "ai_provider_config");

            migrationBuilder.DropTable(
                name: "ai_conversation");
        }
    }
}
