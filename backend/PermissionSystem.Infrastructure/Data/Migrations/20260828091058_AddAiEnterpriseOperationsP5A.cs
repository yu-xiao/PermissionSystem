using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiEnterpriseOperationsP5A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "ai_usage_log",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InputTokenPricePerMillion",
                table: "ai_usage_log",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OutputTokenPricePerMillion",
                table: "ai_usage_log",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingCurrency",
                table: "ai_usage_log",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReservationExpiresAt",
                table: "ai_usage_log",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedCost",
                table: "ai_usage_log",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "ai_usage_log",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouteRole",
                table: "ai_usage_log",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [ai_usage_log] SET [Attempt] = 1, [Round] = [Sequence], [RouteRole] = N'Primary';");

            migrationBuilder.AlterColumn<int>(
                name: "Attempt",
                table: "ai_usage_log",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Round",
                table: "ai_usage_log",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RouteRole",
                table: "ai_usage_log",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FallbackCount",
                table: "ai_run",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalProviderConfigId",
                table: "ai_run",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InputTokenPricePerMillion",
                table: "ai_provider_config",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OutputTokenPricePerMillion",
                table: "ai_provider_config",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingCurrency",
                table: "ai_provider_config",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsJsonSchema",
                table: "ai_provider_config",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsTools",
                table: "ai_provider_config",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ai_budget_policy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MonthlyLimit = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsHardLimit = table.Column<bool>(type: "bit", nullable: false),
                    AlertThresholdPercentage = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ai_budget_policy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_budget_policy_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_model_route_policy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryProviderConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanaryProviderConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CanaryPercentage = table.Column<int>(type: "int", nullable: false),
                    FallbackProviderConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ai_model_route_policy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_model_route_policy_ai_provider_config_CanaryProviderConfigId",
                        column: x => x.CanaryProviderConfigId,
                        principalTable: "ai_provider_config",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_model_route_policy_ai_provider_config_FallbackProviderConfigId",
                        column: x => x.FallbackProviderConfigId,
                        principalTable: "ai_provider_config",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_model_route_policy_ai_provider_config_PrimaryProviderConfigId",
                        column: x => x.PrimaryProviderConfigId,
                        principalTable: "ai_provider_config",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_user_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ai_user_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_user_feedback_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_user_feedback_ai_message_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ai_message",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_user_feedback_ai_run_RunId",
                        column: x => x.RunId,
                        principalTable: "ai_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_log_TenantId_PricingCurrency_CreatedAt",
                table: "ai_usage_log",
                columns: new[] { "TenantId", "PricingCurrency", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_FinalProviderConfigId",
                table: "ai_run",
                column: "FinalProviderConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_FinalProviderConfigId_CreatedAt",
                table: "ai_run",
                columns: new[] { "TenantId", "FinalProviderConfigId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_budget_policy_IsDeleted",
                table: "ai_budget_policy",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_budget_policy_TenantId",
                table: "ai_budget_policy",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_budget_policy_TenantId_PolicyCode",
                table: "ai_budget_policy",
                columns: new[] { "TenantId", "PolicyCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_budget_policy_TenantId_ScopeType_UserId_Currency",
                table: "ai_budget_policy",
                columns: new[] { "TenantId", "ScopeType", "UserId", "Currency" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_budget_policy_TenantId_ScopeType_UserId_IsEnabled",
                table: "ai_budget_policy",
                columns: new[] { "TenantId", "ScopeType", "UserId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_budget_policy_UserId",
                table: "ai_budget_policy",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_route_policy_CanaryProviderConfigId",
                table: "ai_model_route_policy",
                column: "CanaryProviderConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_route_policy_FallbackProviderConfigId",
                table: "ai_model_route_policy",
                column: "FallbackProviderConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_route_policy_IsDeleted",
                table: "ai_model_route_policy",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_route_policy_PrimaryProviderConfigId",
                table: "ai_model_route_policy",
                column: "PrimaryProviderConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_route_policy_TenantId",
                table: "ai_model_route_policy",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_route_policy_TenantId_AgentCode",
                table: "ai_model_route_policy",
                columns: new[] { "TenantId", "AgentCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_route_policy_TenantId_IsEnabled",
                table: "ai_model_route_policy",
                columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_user_feedback_IsDeleted",
                table: "ai_user_feedback",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_user_feedback_MessageId",
                table: "ai_user_feedback",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_user_feedback_RunId",
                table: "ai_user_feedback",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_user_feedback_TenantId",
                table: "ai_user_feedback",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_user_feedback_TenantId_Rating_CreatedAt",
                table: "ai_user_feedback",
                columns: new[] { "TenantId", "Rating", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_user_feedback_TenantId_RunId_UserId",
                table: "ai_user_feedback",
                columns: new[] { "TenantId", "RunId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_user_feedback_UserId",
                table: "ai_user_feedback",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_run_ai_provider_config_FinalProviderConfigId",
                table: "ai_run",
                column: "FinalProviderConfigId",
                principalTable: "ai_provider_config",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_run_ai_provider_config_FinalProviderConfigId",
                table: "ai_run");

            migrationBuilder.DropTable(
                name: "ai_budget_policy");

            migrationBuilder.DropTable(
                name: "ai_model_route_policy");

            migrationBuilder.DropTable(
                name: "ai_user_feedback");

            migrationBuilder.DropIndex(
                name: "IX_ai_usage_log_TenantId_PricingCurrency_CreatedAt",
                table: "ai_usage_log");

            migrationBuilder.DropIndex(
                name: "IX_ai_run_FinalProviderConfigId",
                table: "ai_run");

            migrationBuilder.DropIndex(
                name: "IX_ai_run_TenantId_FinalProviderConfigId_CreatedAt",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "InputTokenPricePerMillion",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "OutputTokenPricePerMillion",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "PricingCurrency",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "ReservationExpiresAt",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "ReservedCost",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "RouteRole",
                table: "ai_usage_log");

            migrationBuilder.DropColumn(
                name: "FallbackCount",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "FinalProviderConfigId",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "InputTokenPricePerMillion",
                table: "ai_provider_config");

            migrationBuilder.DropColumn(
                name: "OutputTokenPricePerMillion",
                table: "ai_provider_config");

            migrationBuilder.DropColumn(
                name: "PricingCurrency",
                table: "ai_provider_config");

            migrationBuilder.DropColumn(
                name: "SupportsJsonSchema",
                table: "ai_provider_config");

            migrationBuilder.DropColumn(
                name: "SupportsTools",
                table: "ai_provider_config");
        }
    }
}
