using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wf_definition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wf_definition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wf_business_binding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wf_business_binding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wf_business_binding_wf_definition_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "wf_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wf_condition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConditionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpressionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_wf_condition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wf_condition_wf_definition_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "wf_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wf_instance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefinitionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    StarterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StarterUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CurrentNodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FormDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wf_instance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wf_instance_wf_definition_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "wf_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wf_node",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NodeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NodeType = table.Column<int>(type: "int", nullable: false),
                    ApproverType = table.Column<int>(type: "int", nullable: true),
                    ApproverIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovalMode = table.Column<int>(type: "int", nullable: true),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PositionX = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PositionY = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_wf_node", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wf_node_wf_definition_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "wf_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wf_edge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromNodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToNodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_wf_edge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wf_edge_wf_condition_ConditionId",
                        column: x => x.ConditionId,
                        principalTable: "wf_condition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wf_edge_wf_definition_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "wf_definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wf_cc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CcUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CcUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wf_cc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wf_cc_wf_instance_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "wf_instance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wf_task",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NodeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wf_task", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wf_task_wf_instance_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "wf_instance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wf_record",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NodeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OperatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperatorUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OperatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wf_record", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wf_record_wf_instance_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "wf_instance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wf_record_wf_task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "wf_task",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_DefinitionId",
                table: "wf_business_binding",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_IsDeleted",
                table: "wf_business_binding",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_TenantId",
                table: "wf_business_binding",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType",
                table: "wf_business_binding",
                columns: new[] { "TenantId", "BusinessType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_TenantId_DefinitionId_IsEnabled",
                table: "wf_business_binding",
                columns: new[] { "TenantId", "DefinitionId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_cc_InstanceId",
                table: "wf_cc",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_cc_IsDeleted",
                table: "wf_cc",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_cc_TenantId",
                table: "wf_cc",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_cc_TenantId_CcUserId_IsRead_CreatedAt",
                table: "wf_cc",
                columns: new[] { "TenantId", "CcUserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_cc_TenantId_InstanceId_CcUserId",
                table: "wf_cc",
                columns: new[] { "TenantId", "InstanceId", "CcUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_condition_DefinitionId",
                table: "wf_condition",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_condition_IsDeleted",
                table: "wf_condition",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_condition_TenantId",
                table: "wf_condition",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_condition_TenantId_DefinitionId_NodeKey_Sort",
                table: "wf_condition",
                columns: new[] { "TenantId", "DefinitionId", "NodeKey", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_definition_IsDeleted",
                table: "wf_definition",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_definition_TenantId",
                table: "wf_definition",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_definition_TenantId_Code_Version",
                table: "wf_definition",
                columns: new[] { "TenantId", "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wf_definition_TenantId_Status_IsPublished",
                table: "wf_definition",
                columns: new[] { "TenantId", "Status", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_edge_ConditionId",
                table: "wf_edge",
                column: "ConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_edge_DefinitionId",
                table: "wf_edge",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_edge_IsDeleted",
                table: "wf_edge",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_edge_TenantId",
                table: "wf_edge",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_edge_TenantId_DefinitionId_FromNodeKey",
                table: "wf_edge",
                columns: new[] { "TenantId", "DefinitionId", "FromNodeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_edge_TenantId_DefinitionId_Sort",
                table: "wf_edge",
                columns: new[] { "TenantId", "DefinitionId", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_edge_TenantId_DefinitionId_ToNodeKey",
                table: "wf_edge",
                columns: new[] { "TenantId", "DefinitionId", "ToNodeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_instance_DefinitionId",
                table: "wf_instance",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_instance_IsDeleted",
                table: "wf_instance",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_instance_TenantId",
                table: "wf_instance",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_instance_TenantId_BusinessType_BusinessId",
                table: "wf_instance",
                columns: new[] { "TenantId", "BusinessType", "BusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_instance_TenantId_StarterUserId_Status_CreatedAt",
                table: "wf_instance",
                columns: new[] { "TenantId", "StarterUserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_instance_TenantId_Status_CreatedAt",
                table: "wf_instance",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_node_DefinitionId",
                table: "wf_node",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_node_IsDeleted",
                table: "wf_node",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_node_TenantId",
                table: "wf_node",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_node_TenantId_DefinitionId_NodeKey",
                table: "wf_node",
                columns: new[] { "TenantId", "DefinitionId", "NodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wf_node_TenantId_DefinitionId_NodeType",
                table: "wf_node",
                columns: new[] { "TenantId", "DefinitionId", "NodeType" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_record_InstanceId",
                table: "wf_record",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_record_IsDeleted",
                table: "wf_record",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_record_TaskId",
                table: "wf_record",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_record_TenantId",
                table: "wf_record",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_record_TenantId_InstanceId_OperatedAt",
                table: "wf_record",
                columns: new[] { "TenantId", "InstanceId", "OperatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_record_TenantId_OperatorUserId_OperatedAt",
                table: "wf_record",
                columns: new[] { "TenantId", "OperatorUserId", "OperatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_task_InstanceId",
                table: "wf_task",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_task_IsDeleted",
                table: "wf_task",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_wf_task_TenantId",
                table: "wf_task",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_wf_task_TenantId_ApproverUserId_Status_CreatedAt",
                table: "wf_task",
                columns: new[] { "TenantId", "ApproverUserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_task_TenantId_InstanceId_NodeKey",
                table: "wf_task",
                columns: new[] { "TenantId", "InstanceId", "NodeKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wf_business_binding");

            migrationBuilder.DropTable(
                name: "wf_cc");

            migrationBuilder.DropTable(
                name: "wf_edge");

            migrationBuilder.DropTable(
                name: "wf_node");

            migrationBuilder.DropTable(
                name: "wf_record");

            migrationBuilder.DropTable(
                name: "wf_condition");

            migrationBuilder.DropTable(
                name: "wf_task");

            migrationBuilder.DropTable(
                name: "wf_instance");

            migrationBuilder.DropTable(
                name: "wf_definition");
        }
    }
}
