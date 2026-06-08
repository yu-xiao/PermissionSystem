using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    public partial class AddStateMachineEngine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StateMachineDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_StateMachineDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StateDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StateCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StateType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StateTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequiredPermission = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConditionJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_StateTransitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StateTransitionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperatorUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateTransitionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_StateMachineDefinitions_IsDeleted", table: "StateMachineDefinitions", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_StateMachineDefinitions_TenantId", table: "StateMachineDefinitions", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_StateMachineDefinitions_TenantId_BusinessType", table: "StateMachineDefinitions", columns: new[] { "TenantId", "BusinessType" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_StateMachineDefinitions_TenantId_IsEnabled", table: "StateMachineDefinitions", columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(name: "IX_StateDefinitions_IsDeleted", table: "StateDefinitions", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_StateDefinitions_TenantId", table: "StateDefinitions", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_StateDefinitions_TenantId_MachineId_Sort", table: "StateDefinitions", columns: new[] { "TenantId", "MachineId", "Sort" });
            migrationBuilder.CreateIndex(name: "IX_StateDefinitions_TenantId_MachineId_StateCode", table: "StateDefinitions", columns: new[] { "TenantId", "MachineId", "StateCode" }, unique: true);

            migrationBuilder.CreateIndex(name: "IX_StateTransitions_IsDeleted", table: "StateTransitions", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_StateTransitions_TenantId", table: "StateTransitions", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_StateTransitions_TenantId_MachineId_FromState_ActionCode", table: "StateTransitions", columns: new[] { "TenantId", "MachineId", "FromState", "ActionCode" });
            migrationBuilder.CreateIndex(name: "IX_StateTransitions_TenantId_MachineId_IsEnabled_Sort", table: "StateTransitions", columns: new[] { "TenantId", "MachineId", "IsEnabled", "Sort" });

            migrationBuilder.CreateIndex(name: "IX_StateTransitionLogs_IsDeleted", table: "StateTransitionLogs", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_StateTransitionLogs_TenantId", table: "StateTransitionLogs", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_StateTransitionLogs_TenantId_BusinessType_ActionCode_CreatedAt", table: "StateTransitionLogs", columns: new[] { "TenantId", "BusinessType", "ActionCode", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_StateTransitionLogs_TenantId_BusinessType_BusinessId_CreatedAt", table: "StateTransitionLogs", columns: new[] { "TenantId", "BusinessType", "BusinessId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StateDefinitions");
            migrationBuilder.DropTable(name: "StateMachineDefinitions");
            migrationBuilder.DropTable(name: "StateTransitionLogs");
            migrationBuilder.DropTable(name: "StateTransitions");
        }
    }
}
