using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAnomalyReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryKey",
                table: "Notifications",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ScheduledTasks_TenantId_Id",
                table: "ScheduledTasks",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "ai_anomaly_rule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContractVersion = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsAnomalous = table.Column<bool>(type: "bit", nullable: false),
                    EpisodeSequence = table.Column<long>(type: "bigint", nullable: false),
                    LastNotifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReservedEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ai_anomaly_rule", x => x.Id);
                    table.UniqueConstraint("AK_ai_anomaly_rule_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ai_anomaly_rule_Contract", "[ContractVersion] = 1 AND [RuleType] = N'DemoPendingCount' AND [EpisodeSequence] >= 0");
                    table.ForeignKey(
                        name: "FK_ai_anomaly_rule_ScheduledTasks_TenantId_ScheduledTaskId",
                        columns: x => new { x.TenantId, x.ScheduledTaskId },
                        principalTable: "ScheduledTasks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_anomaly_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EpisodeSequence = table.Column<long>(type: "bigint", nullable: false),
                    ObservedCount = table.Column<long>(type: "bigint", nullable: false),
                    ScopeFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeliveryKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_ai_anomaly_event", x => x.Id);
                    table.CheckConstraint("CK_ai_anomaly_event_Observation", "[ObservedCount] >= 1 AND [EpisodeSequence] >= 1 AND [AttemptCount] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ai_anomaly_event_ai_anomaly_rule_TenantId_RuleId",
                        columns: x => new { x.TenantId, x.RuleId },
                        principalTable: "ai_anomaly_rule",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_DeliveryKey",
                table: "Notifications",
                columns: new[] { "TenantId", "DeliveryKey" },
                unique: true,
                filter: "[DeliveryKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_event_IsDeleted",
                table: "ai_anomaly_event",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_event_TenantId",
                table: "ai_anomaly_event",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_event_TenantId_DeliveryKey",
                table: "ai_anomaly_event",
                columns: new[] { "TenantId", "DeliveryKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_event_TenantId_RecipientUserId_ObservedAt",
                table: "ai_anomaly_event",
                columns: new[] { "TenantId", "RecipientUserId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_event_TenantId_RuleId_EpisodeSequence",
                table: "ai_anomaly_event",
                columns: new[] { "TenantId", "RuleId", "EpisodeSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_rule_IsDeleted",
                table: "ai_anomaly_rule",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_rule_TenantId",
                table: "ai_anomaly_rule",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_rule_TenantId_OwnerUserId",
                table: "ai_anomaly_rule",
                columns: new[] { "TenantId", "OwnerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_anomaly_rule_TenantId_ScheduledTaskId",
                table: "ai_anomaly_rule",
                columns: new[] { "TenantId", "ScheduledTaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_anomaly_event");

            migrationBuilder.DropTable(
                name: "ai_anomaly_rule");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ScheduledTasks_TenantId_Id",
                table: "ScheduledTasks");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId_DeliveryKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeliveryKey",
                table: "Notifications");
        }
    }
}
