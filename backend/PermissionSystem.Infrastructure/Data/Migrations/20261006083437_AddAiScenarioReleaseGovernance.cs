using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiScenarioReleaseGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuildIdentity",
                table: "ai_run",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionConfigurationHash",
                table: "ai_run",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionConfigurationJson",
                table: "ai_run",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScenarioContentHash",
                table: "ai_run",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScenarioId",
                table: "ai_run",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScenarioVersionId",
                table: "ai_run",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScenarioId",
                table: "ai_conversation",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScenarioVersionId",
                table: "ai_conversation",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_scenario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ai_scenario", x => x.Id);
                    table.UniqueConstraint("AK_ai_scenario_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ai_scenario_draft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ai_scenario_draft", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_scenario_draft_ai_scenario_TenantId_ScenarioId",
                        columns: x => new { x.TenantId, x.ScenarioId },
                        principalTable: "ai_scenario",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_scenario_version",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BuildIdentity = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ai_scenario_version", x => x.Id);
                    table.UniqueConstraint("AK_ai_scenario_version_TenantId_ScenarioId_Id", x => new { x.TenantId, x.ScenarioId, x.Id });
                    table.ForeignKey(
                        name: "FK_ai_scenario_version_ai_scenario_TenantId_ScenarioId",
                        columns: x => new { x.TenantId, x.ScenarioId },
                        principalTable: "ai_scenario",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_scenario_evaluation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReportJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AutomaticPassed = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ai_scenario_evaluation", x => x.Id);
                    table.UniqueConstraint("AK_ai_scenario_evaluation_TenantId_ScenarioId_VersionId_Id", x => new { x.TenantId, x.ScenarioId, x.VersionId, x.Id });
                    table.ForeignKey(
                        name: "FK_ai_scenario_evaluation_ai_scenario_version_TenantId_ScenarioId_VersionId",
                        columns: x => new { x.TenantId, x.ScenarioId, x.VersionId },
                        principalTable: "ai_scenario_version",
                        principalColumns: new[] { "TenantId", "ScenarioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_scenario_release_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReviewJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QualificationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ai_scenario_release_event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_scenario_release_event_ai_scenario_evaluation_TenantId_ScenarioId_VersionId_EvaluationId",
                        columns: x => new { x.TenantId, x.ScenarioId, x.VersionId, x.EvaluationId },
                        principalTable: "ai_scenario_evaluation",
                        principalColumns: new[] { "TenantId", "ScenarioId", "VersionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_scenario_release_event_ai_scenario_version_TenantId_ScenarioId_PreviousVersionId",
                        columns: x => new { x.TenantId, x.ScenarioId, x.PreviousVersionId },
                        principalTable: "ai_scenario_version",
                        principalColumns: new[] { "TenantId", "ScenarioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_scenario_release_event_ai_scenario_version_TenantId_ScenarioId_VersionId",
                        columns: x => new { x.TenantId, x.ScenarioId, x.VersionId },
                        principalTable: "ai_scenario_version",
                        principalColumns: new[] { "TenantId", "ScenarioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_run_TenantId_ScenarioId_ScenarioVersionId",
                table: "ai_run",
                columns: new[] { "TenantId", "ScenarioId", "ScenarioVersionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ai_run_ScenarioVersion",
                table: "ai_run",
                sql: "([ScenarioId] IS NULL AND [ScenarioVersionId] IS NULL) OR ([ScenarioId] IS NOT NULL AND [ScenarioVersionId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_TenantId_ScenarioId_ScenarioVersionId",
                table: "ai_conversation",
                columns: new[] { "TenantId", "ScenarioId", "ScenarioVersionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ai_conversation_ScenarioVersion",
                table: "ai_conversation",
                sql: "([ScenarioId] IS NULL AND [ScenarioVersionId] IS NULL) OR ([ScenarioId] IS NOT NULL AND [ScenarioVersionId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_IsDeleted",
                table: "ai_scenario",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_TenantId",
                table: "ai_scenario",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_TenantId_Code",
                table: "ai_scenario",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_TenantId_Id_CurrentVersionId",
                table: "ai_scenario",
                columns: new[] { "TenantId", "Id", "CurrentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_draft_IsDeleted",
                table: "ai_scenario_draft",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_draft_TenantId",
                table: "ai_scenario_draft",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_draft_TenantId_ScenarioId",
                table: "ai_scenario_draft",
                columns: new[] { "TenantId", "ScenarioId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_evaluation_IsDeleted",
                table: "ai_scenario_evaluation",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_evaluation_TenantId",
                table: "ai_scenario_evaluation",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_evaluation_TenantId_VersionId_ReportHash",
                table: "ai_scenario_evaluation",
                columns: new[] { "TenantId", "VersionId", "ReportHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_release_event_IsDeleted",
                table: "ai_scenario_release_event",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_release_event_TenantId",
                table: "ai_scenario_release_event",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_release_event_TenantId_ScenarioId_PreviousVersionId",
                table: "ai_scenario_release_event",
                columns: new[] { "TenantId", "ScenarioId", "PreviousVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_release_event_TenantId_ScenarioId_Sequence",
                table: "ai_scenario_release_event",
                columns: new[] { "TenantId", "ScenarioId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_release_event_TenantId_ScenarioId_VersionId_EvaluationId",
                table: "ai_scenario_release_event",
                columns: new[] { "TenantId", "ScenarioId", "VersionId", "EvaluationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_release_event_TenantId_VersionId_CreatedAt",
                table: "ai_scenario_release_event",
                columns: new[] { "TenantId", "VersionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_version_IsDeleted",
                table: "ai_scenario_version",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_version_TenantId",
                table: "ai_scenario_version",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_version_TenantId_ScenarioId_ContentHash",
                table: "ai_scenario_version",
                columns: new[] { "TenantId", "ScenarioId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_scenario_version_TenantId_ScenarioId_VersionNumber",
                table: "ai_scenario_version",
                columns: new[] { "TenantId", "ScenarioId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_conversation_ai_scenario_version_TenantId_ScenarioId_ScenarioVersionId",
                table: "ai_conversation",
                columns: new[] { "TenantId", "ScenarioId", "ScenarioVersionId" },
                principalTable: "ai_scenario_version",
                principalColumns: new[] { "TenantId", "ScenarioId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_run_ai_scenario_version_TenantId_ScenarioId_ScenarioVersionId",
                table: "ai_run",
                columns: new[] { "TenantId", "ScenarioId", "ScenarioVersionId" },
                principalTable: "ai_scenario_version",
                principalColumns: new[] { "TenantId", "ScenarioId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_scenario_ai_scenario_version_TenantId_Id_CurrentVersionId",
                table: "ai_scenario",
                columns: new[] { "TenantId", "Id", "CurrentVersionId" },
                principalTable: "ai_scenario_version",
                principalColumns: new[] { "TenantId", "ScenarioId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_conversation_ai_scenario_version_TenantId_ScenarioId_ScenarioVersionId",
                table: "ai_conversation");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_run_ai_scenario_version_TenantId_ScenarioId_ScenarioVersionId",
                table: "ai_run");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_scenario_ai_scenario_version_TenantId_Id_CurrentVersionId",
                table: "ai_scenario");

            migrationBuilder.DropTable(
                name: "ai_scenario_draft");

            migrationBuilder.DropTable(
                name: "ai_scenario_release_event");

            migrationBuilder.DropTable(
                name: "ai_scenario_evaluation");

            migrationBuilder.DropTable(
                name: "ai_scenario_version");

            migrationBuilder.DropTable(
                name: "ai_scenario");

            migrationBuilder.DropIndex(
                name: "IX_ai_run_TenantId_ScenarioId_ScenarioVersionId",
                table: "ai_run");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ai_run_ScenarioVersion",
                table: "ai_run");

            migrationBuilder.DropIndex(
                name: "IX_ai_conversation_TenantId_ScenarioId_ScenarioVersionId",
                table: "ai_conversation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ai_conversation_ScenarioVersion",
                table: "ai_conversation");

            migrationBuilder.DropColumn(
                name: "BuildIdentity",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ExecutionConfigurationHash",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ExecutionConfigurationJson",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ScenarioContentHash",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ScenarioId",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ScenarioVersionId",
                table: "ai_run");

            migrationBuilder.DropColumn(
                name: "ScenarioId",
                table: "ai_conversation");

            migrationBuilder.DropColumn(
                name: "ScenarioVersionId",
                table: "ai_conversation");
        }
    }
}
