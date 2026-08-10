using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EA022SoftDeleteConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @Ea022ActiveChecks TABLE
                (
                    [CheckName] nvarchar(256) NOT NULL,
                    [TableName] sysname NOT NULL,
                    [KeyColumns] nvarchar(1000) NOT NULL,
                    [Predicate] nvarchar(500) NOT NULL
                );

                INSERT INTO @Ea022ActiveChecks ([CheckName], [TableName], [KeyColumns], [Predicate])
                VALUES
                    (N'wf_node tenant/definition/node key', N'wf_node', N'[TenantId], [DefinitionId], [NodeKey]', N'[IsDeleted] = 0'),
                    (N'wf_definition tenant/code/version', N'wf_definition', N'[TenantId], [Code], [Version]', N'[IsDeleted] = 0'),
                    (N'wf_business_binding tenant/business type', N'wf_business_binding', N'[TenantId], [BusinessType]', N'[IsDeleted] = 0'),
                    (N'Users tenant/user name', N'Users', N'[TenantId], [NormalizedUserName]', N'[IsDeleted] = 0'),
                    (N'UserRoles tenant/user/role', N'UserRoles', N'[TenantId], [UserId], [RoleId]', N'[IsDeleted] = 0'),
                    (N'UserDataScopes tenant/user', N'UserDataScopes', N'[TenantId], [UserId]', N'[IsDeleted] = 0'),
                    (N'SystemConfigs tenant/config key', N'SystemConfigs', N'[TenantId], [ConfigKey]', N'[IsDeleted] = 0'),
                    (N'StateMachineDefinitions tenant/business type', N'StateMachineDefinitions', N'[TenantId], [BusinessType]', N'[IsDeleted] = 0'),
                    (N'StateDefinitions tenant/machine/state code', N'StateDefinitions', N'[TenantId], [MachineId], [StateCode]', N'[IsDeleted] = 0'),
                    (N'sso_user_binding external user', N'sso_user_binding', N'[TenantId], [ProviderId], [ExternalUserId]', N'[IsDeleted] = 0'),
                    (N'sso_user_binding local user', N'sso_user_binding', N'[TenantId], [ProviderId], [LocalUserId]', N'[IsDeleted] = 0'),
                    (N'sso_role_mapping', N'sso_role_mapping', N'[TenantId], [ProviderId], [ExternalRole], [LocalRoleId]', N'[IsDeleted] = 0'),
                    (N'sso_provider tenant/provider code', N'sso_provider', N'[TenantId], [ProviderCode]', N'[IsDeleted] = 0'),
                    (N'sso_department_mapping', N'sso_department_mapping', N'[TenantId], [ProviderId], [ExternalDepartment], [LocalDepartmentId]', N'[IsDeleted] = 0'),
                    (N'ScheduledTasks tenant/code', N'ScheduledTasks', N'[TenantId], [Code]', N'[IsDeleted] = 0'),
                    (N'Roles tenant/code', N'Roles', N'[TenantId], [Code]', N'[IsDeleted] = 0'),
                    (N'RolePermissions tenant/role/permission', N'RolePermissions', N'[TenantId], [RoleId], [PermissionId]', N'[IsDeleted] = 0'),
                    (N'RoleMenus tenant/role/menu', N'RoleMenus', N'[TenantId], [RoleId], [MenuId]', N'[IsDeleted] = 0'),
                    (N'RoleDataScopes tenant/role', N'RoleDataScopes', N'[TenantId], [RoleId]', N'[IsDeleted] = 0'),
                    (N'ReportQueryParams tenant/report/param', N'ReportQueryParams', N'[TenantId], [ReportId], [ParamCode]', N'[IsDeleted] = 0'),
                    (N'ReportDefinitions tenant/report code', N'ReportDefinitions', N'[TenantId], [ReportCode]', N'[IsDeleted] = 0'),
                    (N'PrintTemplates tenant/template code', N'PrintTemplates', N'[TenantId], [TemplateCode]', N'[IsDeleted] = 0'),
                    (N'Permissions tenant/code', N'Permissions', N'[TenantId], [Code]', N'[IsDeleted] = 0'),
                    (N'NumberRules tenant/rule code', N'NumberRules', N'[TenantId], [RuleCode]', N'[IsDeleted] = 0'),
                    (N'NotificationTemplates tenant/code', N'NotificationTemplates', N'[TenantId], [Code]', N'[IsDeleted] = 0'),
                    (N'LoginFailureRecords tenant/user/IP', N'LoginFailureRecords', N'[TenantId], [UserName], [IpAddress]', N'[IsDeleted] = 0 AND [IpAddress] IS NOT NULL'),
                    (N'IpAccessRules tenant/type/pattern', N'IpAccessRules', N'[TenantId], [RuleType], [IpPattern]', N'[IsDeleted] = 0'),
                    (N'DictionaryTypes tenant/code', N'DictionaryTypes', N'[TenantId], [Code]', N'[IsDeleted] = 0'),
                    (N'DictionaryItems tenant/type/value', N'DictionaryItems', N'[TenantId], [TypeCode], [Value]', N'[IsDeleted] = 0'),
                    (N'Departments tenant/code', N'Departments', N'[TenantId], [Code]', N'[IsDeleted] = 0'),
                    (N'demo_business_order tenant/order number', N'demo_business_order', N'[TenantId], [OrderNo]', N'[IsDeleted] = 0'),
                    (N'demo_approval_order tenant/order number', N'demo_approval_order', N'[TenantId], [OrderNo]', N'[IsDeleted] = 0'),
                    (N'ApiClients tenant/client code', N'ApiClients', N'[TenantId], [ClientCode]', N'[IsDeleted] = 0');

                DECLARE @Ea022CheckName nvarchar(256);
                DECLARE @Ea022TableName sysname;
                DECLARE @Ea022KeyColumns nvarchar(1000);
                DECLARE @Ea022Predicate nvarchar(500);
                DECLARE @Ea022Sql nvarchar(max);

                DECLARE Ea022ActiveCheckCursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT [CheckName], [TableName], [KeyColumns], [Predicate]
                    FROM @Ea022ActiveChecks;

                OPEN Ea022ActiveCheckCursor;
                FETCH NEXT FROM Ea022ActiveCheckCursor
                    INTO @Ea022CheckName, @Ea022TableName, @Ea022KeyColumns, @Ea022Predicate;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @Ea022Sql =
                        N'IF EXISTS (SELECT 1 FROM [dbo].' + QUOTENAME(@Ea022TableName) +
                        N' WHERE ' + @Ea022Predicate + N' GROUP BY ' + @Ea022KeyColumns +
                        N' HAVING COUNT_BIG(*) > 1) THROW 51000, N''EA-022 migration blocked: duplicate active key in ' +
                        REPLACE(@Ea022CheckName, N'''', N'''''') + N'.'', 1;';

                    EXEC sys.sp_executesql @Ea022Sql;

                    FETCH NEXT FROM Ea022ActiveCheckCursor
                        INTO @Ea022CheckName, @Ea022TableName, @Ea022KeyColumns, @Ea022Predicate;
                END;

                CLOSE Ea022ActiveCheckCursor;
                DEALLOCATE Ea022ActiveCheckCursor;
                """);

            migrationBuilder.DropIndex(
                name: "IX_wf_node_TenantId_DefinitionId_NodeKey",
                table: "wf_node");

            migrationBuilder.DropIndex(
                name: "IX_wf_definition_TenantId_Code_Version",
                table: "wf_definition");

            migrationBuilder.DropIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType_IsDeleted",
                table: "wf_business_binding");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_NormalizedUserName",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserDataScopes_TenantId_UserId",
                table: "UserDataScopes");

            migrationBuilder.DropIndex(
                name: "IX_SystemConfigs_TenantId_ConfigKey",
                table: "SystemConfigs");

            migrationBuilder.DropIndex(
                name: "IX_StateMachineDefinitions_TenantId_BusinessType",
                table: "StateMachineDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_StateDefinitions_TenantId_MachineId_StateCode",
                table: "StateDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_ExternalUserId",
                table: "sso_user_binding");

            migrationBuilder.DropIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_LocalUserId",
                table: "sso_user_binding");

            migrationBuilder.DropIndex(
                name: "IX_sso_role_mapping_TenantId_ProviderId_ExternalRole_LocalRoleId",
                table: "sso_role_mapping");

            migrationBuilder.DropIndex(
                name: "IX_sso_provider_TenantId_ProviderCode",
                table: "sso_provider");

            migrationBuilder.DropIndex(
                name: "IX_sso_department_mapping_TenantId_ProviderId_ExternalDepartment_LocalDepartmentId",
                table: "sso_department_mapping");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTasks_TenantId_Code",
                table: "ScheduledTasks");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Code",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_TenantId_RoleId_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RoleMenus_TenantId_RoleId_MenuId",
                table: "RoleMenus");

            migrationBuilder.DropIndex(
                name: "IX_RoleDataScopes_TenantId_RoleId",
                table: "RoleDataScopes");

            migrationBuilder.DropIndex(
                name: "IX_ReportQueryParams_TenantId_ReportId_ParamCode",
                table: "ReportQueryParams");

            migrationBuilder.DropIndex(
                name: "IX_ReportDefinitions_TenantId_ReportCode",
                table: "ReportDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PrintTemplates_TenantId_TemplateCode",
                table: "PrintTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_TenantId_Code",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_NumberRules_TenantId_RuleCode",
                table: "NumberRules");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTemplates_TenantId_Code",
                table: "NotificationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_LoginFailureRecords_TenantId_UserName_IpAddress",
                table: "LoginFailureRecords");

            migrationBuilder.DropIndex(
                name: "IX_IpAccessRules_TenantId_RuleType_IpPattern",
                table: "IpAccessRules");

            migrationBuilder.DropIndex(
                name: "IX_DictionaryTypes_TenantId_Code",
                table: "DictionaryTypes");

            migrationBuilder.DropIndex(
                name: "IX_DictionaryItems_TenantId_TypeCode_Value",
                table: "DictionaryItems");

            migrationBuilder.DropIndex(
                name: "IX_Departments_TenantId_Code",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_demo_business_order_TenantId_OrderNo_IsDeleted",
                table: "demo_business_order");

            migrationBuilder.DropIndex(
                name: "IX_demo_approval_order_TenantId_OrderNo_IsDeleted",
                table: "demo_approval_order");

            migrationBuilder.DropIndex(
                name: "IX_ApiClients_TenantId_ClientCode",
                table: "ApiClients");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_record",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_node",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_edge",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_definition",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_condition",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_cc",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_business_binding",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WebhookSubscriptions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WebhookDeliveryLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UserSessions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UserRoles",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UserNotifications",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UserDataScopes",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SystemConfigs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StateTransitions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StateTransitionLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StateMachineDefinitions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StateDefinitions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "sso_user_binding",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "sso_role_mapping",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "sso_provider",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "sso_login_log",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "sso_department_mapping",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SecurityPolicies",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ScheduledTasks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ScheduledTaskExecutionLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Roles",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RolePermissions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RoleMenus",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RoleDataScopes",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReportQueryParams",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReportExecutionLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReportDefinitions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PrintTemplates",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PrintRecords",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Permissions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "OutboxMessages",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "OperationLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "NumberSequences",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "NumberRuleSegments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "NumberRules",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "NotificationTemplates",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Notifications",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Menus",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LoginLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LoginFailureRecords",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "JobExecutionLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "IpAccessRules",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InboxMessages",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "FileResources",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ExternalApiCallLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DictionaryTypes",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DictionaryItems",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Departments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ApiClientSecrets",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ApiClients",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_wf_node_TenantId_DefinitionId_NodeKey",
                table: "wf_node",
                columns: new[] { "TenantId", "DefinitionId", "NodeKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_wf_definition_TenantId_Code_Version",
                table: "wf_definition",
                columns: new[] { "TenantId", "Code", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType",
                table: "wf_business_binding",
                columns: new[] { "TenantId", "BusinessType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_NormalizedUserName",
                table: "Users",
                columns: new[] { "TenantId", "NormalizedUserName" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserDataScopes_TenantId_UserId",
                table: "UserDataScopes",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_TenantId_ConfigKey",
                table: "SystemConfigs",
                columns: new[] { "TenantId", "ConfigKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StateMachineDefinitions_TenantId_BusinessType",
                table: "StateMachineDefinitions",
                columns: new[] { "TenantId", "BusinessType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StateDefinitions_TenantId_MachineId_StateCode",
                table: "StateDefinitions",
                columns: new[] { "TenantId", "MachineId", "StateCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_ExternalUserId",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ProviderId", "ExternalUserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_LocalUserId",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ProviderId", "LocalUserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_sso_role_mapping_TenantId_ProviderId_ExternalRole_LocalRoleId",
                table: "sso_role_mapping",
                columns: new[] { "TenantId", "ProviderId", "ExternalRole", "LocalRoleId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_sso_provider_TenantId_ProviderCode",
                table: "sso_provider",
                columns: new[] { "TenantId", "ProviderCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_sso_department_mapping_TenantId_ProviderId_ExternalDepartment_LocalDepartmentId",
                table: "sso_department_mapping",
                columns: new[] { "TenantId", "ProviderId", "ExternalDepartment", "LocalDepartmentId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_TenantId_Code",
                table: "ScheduledTasks",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Code",
                table: "Roles",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "TenantId", "RoleId", "PermissionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RoleMenus_TenantId_RoleId_MenuId",
                table: "RoleMenus",
                columns: new[] { "TenantId", "RoleId", "MenuId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RoleDataScopes_TenantId_RoleId",
                table: "RoleDataScopes",
                columns: new[] { "TenantId", "RoleId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReportQueryParams_TenantId_ReportId_ParamCode",
                table: "ReportQueryParams",
                columns: new[] { "TenantId", "ReportId", "ParamCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_TenantId_ReportCode",
                table: "ReportDefinitions",
                columns: new[] { "TenantId", "ReportCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_TenantId_TemplateCode",
                table: "PrintTemplates",
                columns: new[] { "TenantId", "TemplateCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId_Code",
                table: "Permissions",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NumberRules_TenantId_RuleCode",
                table: "NumberRules",
                columns: new[] { "TenantId", "RuleCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_TenantId_Code",
                table: "NotificationTemplates",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LoginFailureRecords_TenantId_UserName_IpAddress",
                table: "LoginFailureRecords",
                columns: new[] { "TenantId", "UserName", "IpAddress" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IpAddress] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IpAccessRules_TenantId_RuleType_IpPattern",
                table: "IpAccessRules",
                columns: new[] { "TenantId", "RuleType", "IpPattern" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryTypes_TenantId_Code",
                table: "DictionaryTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryItems_TenantId_TypeCode_Value",
                table: "DictionaryItems",
                columns: new[] { "TenantId", "TypeCode", "Value" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId_Code",
                table: "Departments",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_TenantId_OrderNo",
                table: "demo_business_order",
                columns: new[] { "TenantId", "OrderNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_demo_approval_order_TenantId_OrderNo",
                table: "demo_approval_order",
                columns: new[] { "TenantId", "OrderNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ApiClients_TenantId_ClientCode",
                table: "ApiClients",
                columns: new[] { "TenantId", "ClientCode" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @Ea022RollbackChecks TABLE
                (
                    [CheckName] nvarchar(256) NOT NULL,
                    [TableName] sysname NOT NULL,
                    [KeyColumns] nvarchar(1000) NOT NULL,
                    [Predicate] nvarchar(500) NOT NULL
                );

                INSERT INTO @Ea022RollbackChecks ([CheckName], [TableName], [KeyColumns], [Predicate])
                VALUES
                    (N'wf_node tenant/definition/node key', N'wf_node', N'[TenantId], [DefinitionId], [NodeKey]', N'1 = 1'),
                    (N'wf_definition tenant/code/version', N'wf_definition', N'[TenantId], [Code], [Version]', N'1 = 1'),
                    (N'wf_business_binding tenant/business type/deletion state', N'wf_business_binding', N'[TenantId], [BusinessType], [IsDeleted]', N'1 = 1'),
                    (N'Users tenant/user name', N'Users', N'[TenantId], [NormalizedUserName]', N'1 = 1'),
                    (N'UserRoles tenant/user/role', N'UserRoles', N'[TenantId], [UserId], [RoleId]', N'1 = 1'),
                    (N'UserDataScopes tenant/user', N'UserDataScopes', N'[TenantId], [UserId]', N'1 = 1'),
                    (N'SystemConfigs tenant/config key', N'SystemConfigs', N'[TenantId], [ConfigKey]', N'1 = 1'),
                    (N'StateMachineDefinitions tenant/business type', N'StateMachineDefinitions', N'[TenantId], [BusinessType]', N'1 = 1'),
                    (N'StateDefinitions tenant/machine/state code', N'StateDefinitions', N'[TenantId], [MachineId], [StateCode]', N'1 = 1'),
                    (N'sso_user_binding external user', N'sso_user_binding', N'[TenantId], [ProviderId], [ExternalUserId]', N'1 = 1'),
                    (N'sso_user_binding local user', N'sso_user_binding', N'[TenantId], [ProviderId], [LocalUserId]', N'1 = 1'),
                    (N'sso_role_mapping', N'sso_role_mapping', N'[TenantId], [ProviderId], [ExternalRole], [LocalRoleId]', N'1 = 1'),
                    (N'sso_provider tenant/provider code', N'sso_provider', N'[TenantId], [ProviderCode]', N'1 = 1'),
                    (N'sso_department_mapping', N'sso_department_mapping', N'[TenantId], [ProviderId], [ExternalDepartment], [LocalDepartmentId]', N'1 = 1'),
                    (N'ScheduledTasks tenant/code', N'ScheduledTasks', N'[TenantId], [Code]', N'1 = 1'),
                    (N'Roles tenant/code', N'Roles', N'[TenantId], [Code]', N'1 = 1'),
                    (N'RolePermissions tenant/role/permission', N'RolePermissions', N'[TenantId], [RoleId], [PermissionId]', N'1 = 1'),
                    (N'RoleMenus tenant/role/menu', N'RoleMenus', N'[TenantId], [RoleId], [MenuId]', N'1 = 1'),
                    (N'RoleDataScopes tenant/role', N'RoleDataScopes', N'[TenantId], [RoleId]', N'1 = 1'),
                    (N'ReportQueryParams tenant/report/param', N'ReportQueryParams', N'[TenantId], [ReportId], [ParamCode]', N'1 = 1'),
                    (N'ReportDefinitions tenant/report code', N'ReportDefinitions', N'[TenantId], [ReportCode]', N'1 = 1'),
                    (N'PrintTemplates tenant/template code', N'PrintTemplates', N'[TenantId], [TemplateCode]', N'1 = 1'),
                    (N'Permissions tenant/code', N'Permissions', N'[TenantId], [Code]', N'1 = 1'),
                    (N'NumberRules tenant/rule code', N'NumberRules', N'[TenantId], [RuleCode]', N'1 = 1'),
                    (N'NotificationTemplates tenant/code', N'NotificationTemplates', N'[TenantId], [Code]', N'1 = 1'),
                    (N'LoginFailureRecords tenant/user/IP', N'LoginFailureRecords', N'[TenantId], [UserName], [IpAddress]', N'[IpAddress] IS NOT NULL'),
                    (N'IpAccessRules tenant/type/pattern', N'IpAccessRules', N'[TenantId], [RuleType], [IpPattern]', N'1 = 1'),
                    (N'DictionaryTypes tenant/code', N'DictionaryTypes', N'[TenantId], [Code]', N'1 = 1'),
                    (N'DictionaryItems tenant/type/value', N'DictionaryItems', N'[TenantId], [TypeCode], [Value]', N'1 = 1'),
                    (N'Departments tenant/code', N'Departments', N'[TenantId], [Code]', N'1 = 1'),
                    (N'demo_business_order tenant/order number/deletion state', N'demo_business_order', N'[TenantId], [OrderNo], [IsDeleted]', N'1 = 1'),
                    (N'demo_approval_order tenant/order number/deletion state', N'demo_approval_order', N'[TenantId], [OrderNo], [IsDeleted]', N'1 = 1'),
                    (N'ApiClients tenant/client code', N'ApiClients', N'[TenantId], [ClientCode]', N'1 = 1');

                DECLARE @Ea022CheckName nvarchar(256);
                DECLARE @Ea022TableName sysname;
                DECLARE @Ea022KeyColumns nvarchar(1000);
                DECLARE @Ea022Predicate nvarchar(500);
                DECLARE @Ea022Sql nvarchar(max);

                DECLARE Ea022RollbackCheckCursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT [CheckName], [TableName], [KeyColumns], [Predicate]
                    FROM @Ea022RollbackChecks;

                OPEN Ea022RollbackCheckCursor;
                FETCH NEXT FROM Ea022RollbackCheckCursor
                    INTO @Ea022CheckName, @Ea022TableName, @Ea022KeyColumns, @Ea022Predicate;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @Ea022Sql =
                        N'IF EXISTS (SELECT 1 FROM [dbo].' + QUOTENAME(@Ea022TableName) +
                        N' WHERE ' + @Ea022Predicate + N' GROUP BY ' + @Ea022KeyColumns +
                        N' HAVING COUNT_BIG(*) > 1) THROW 51000, N''EA-022 rollback blocked: historical key reuse exists in ' +
                        REPLACE(@Ea022CheckName, N'''', N'''''') + N'. Resolve duplicates before rollback.'', 1;';

                    EXEC sys.sp_executesql @Ea022Sql;

                    FETCH NEXT FROM Ea022RollbackCheckCursor
                        INTO @Ea022CheckName, @Ea022TableName, @Ea022KeyColumns, @Ea022Predicate;
                END;

                CLOSE Ea022RollbackCheckCursor;
                DEALLOCATE Ea022RollbackCheckCursor;
                """);

            migrationBuilder.DropIndex(
                name: "IX_wf_node_TenantId_DefinitionId_NodeKey",
                table: "wf_node");

            migrationBuilder.DropIndex(
                name: "IX_wf_definition_TenantId_Code_Version",
                table: "wf_definition");

            migrationBuilder.DropIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType",
                table: "wf_business_binding");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_NormalizedUserName",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserDataScopes_TenantId_UserId",
                table: "UserDataScopes");

            migrationBuilder.DropIndex(
                name: "IX_SystemConfigs_TenantId_ConfigKey",
                table: "SystemConfigs");

            migrationBuilder.DropIndex(
                name: "IX_StateMachineDefinitions_TenantId_BusinessType",
                table: "StateMachineDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_StateDefinitions_TenantId_MachineId_StateCode",
                table: "StateDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_ExternalUserId",
                table: "sso_user_binding");

            migrationBuilder.DropIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_LocalUserId",
                table: "sso_user_binding");

            migrationBuilder.DropIndex(
                name: "IX_sso_role_mapping_TenantId_ProviderId_ExternalRole_LocalRoleId",
                table: "sso_role_mapping");

            migrationBuilder.DropIndex(
                name: "IX_sso_provider_TenantId_ProviderCode",
                table: "sso_provider");

            migrationBuilder.DropIndex(
                name: "IX_sso_department_mapping_TenantId_ProviderId_ExternalDepartment_LocalDepartmentId",
                table: "sso_department_mapping");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTasks_TenantId_Code",
                table: "ScheduledTasks");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Code",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_TenantId_RoleId_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RoleMenus_TenantId_RoleId_MenuId",
                table: "RoleMenus");

            migrationBuilder.DropIndex(
                name: "IX_RoleDataScopes_TenantId_RoleId",
                table: "RoleDataScopes");

            migrationBuilder.DropIndex(
                name: "IX_ReportQueryParams_TenantId_ReportId_ParamCode",
                table: "ReportQueryParams");

            migrationBuilder.DropIndex(
                name: "IX_ReportDefinitions_TenantId_ReportCode",
                table: "ReportDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PrintTemplates_TenantId_TemplateCode",
                table: "PrintTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_TenantId_Code",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_NumberRules_TenantId_RuleCode",
                table: "NumberRules");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTemplates_TenantId_Code",
                table: "NotificationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_LoginFailureRecords_TenantId_UserName_IpAddress",
                table: "LoginFailureRecords");

            migrationBuilder.DropIndex(
                name: "IX_IpAccessRules_TenantId_RuleType_IpPattern",
                table: "IpAccessRules");

            migrationBuilder.DropIndex(
                name: "IX_DictionaryTypes_TenantId_Code",
                table: "DictionaryTypes");

            migrationBuilder.DropIndex(
                name: "IX_DictionaryItems_TenantId_TypeCode_Value",
                table: "DictionaryItems");

            migrationBuilder.DropIndex(
                name: "IX_Departments_TenantId_Code",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_demo_business_order_TenantId_OrderNo",
                table: "demo_business_order");

            migrationBuilder.DropIndex(
                name: "IX_demo_approval_order_TenantId_OrderNo",
                table: "demo_approval_order");

            migrationBuilder.DropIndex(
                name: "IX_ApiClients_TenantId_ClientCode",
                table: "ApiClients");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_record");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_node");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_edge");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_definition");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_condition");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_cc");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_business_binding");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UserDataScopes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SystemConfigs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StateTransitions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StateTransitionLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StateMachineDefinitions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StateDefinitions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "sso_user_binding");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "sso_role_mapping");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "sso_provider");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "sso_login_log");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "sso_department_mapping");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SecurityPolicies");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ScheduledTaskExecutionLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RoleMenus");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RoleDataScopes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReportQueryParams");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReportExecutionLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReportDefinitions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PrintTemplates");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PrintRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "OperationLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "NumberSequences");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "NumberRuleSegments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "NumberRules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Menus");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LoginLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LoginFailureRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "JobExecutionLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "IpAccessRules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ExternalApiCallLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DictionaryTypes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DictionaryItems");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ApiClientSecrets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ApiClients");

            migrationBuilder.CreateIndex(
                name: "IX_wf_node_TenantId_DefinitionId_NodeKey",
                table: "wf_node",
                columns: new[] { "TenantId", "DefinitionId", "NodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wf_definition_TenantId_Code_Version",
                table: "wf_definition",
                columns: new[] { "TenantId", "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType_IsDeleted",
                table: "wf_business_binding",
                columns: new[] { "TenantId", "BusinessType", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_NormalizedUserName",
                table: "Users",
                columns: new[] { "TenantId", "NormalizedUserName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDataScopes_TenantId_UserId",
                table: "UserDataScopes",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_TenantId_ConfigKey",
                table: "SystemConfigs",
                columns: new[] { "TenantId", "ConfigKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StateMachineDefinitions_TenantId_BusinessType",
                table: "StateMachineDefinitions",
                columns: new[] { "TenantId", "BusinessType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StateDefinitions_TenantId_MachineId_StateCode",
                table: "StateDefinitions",
                columns: new[] { "TenantId", "MachineId", "StateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_ExternalUserId",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ProviderId", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_user_binding_TenantId_ProviderId_LocalUserId",
                table: "sso_user_binding",
                columns: new[] { "TenantId", "ProviderId", "LocalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_role_mapping_TenantId_ProviderId_ExternalRole_LocalRoleId",
                table: "sso_role_mapping",
                columns: new[] { "TenantId", "ProviderId", "ExternalRole", "LocalRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_provider_TenantId_ProviderCode",
                table: "sso_provider",
                columns: new[] { "TenantId", "ProviderCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_department_mapping_TenantId_ProviderId_ExternalDepartment_LocalDepartmentId",
                table: "sso_department_mapping",
                columns: new[] { "TenantId", "ProviderId", "ExternalDepartment", "LocalDepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_TenantId_Code",
                table: "ScheduledTasks",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Code",
                table: "Roles",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "TenantId", "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleMenus_TenantId_RoleId_MenuId",
                table: "RoleMenus",
                columns: new[] { "TenantId", "RoleId", "MenuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleDataScopes_TenantId_RoleId",
                table: "RoleDataScopes",
                columns: new[] { "TenantId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportQueryParams_TenantId_ReportId_ParamCode",
                table: "ReportQueryParams",
                columns: new[] { "TenantId", "ReportId", "ParamCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_TenantId_ReportCode",
                table: "ReportDefinitions",
                columns: new[] { "TenantId", "ReportCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_TenantId_TemplateCode",
                table: "PrintTemplates",
                columns: new[] { "TenantId", "TemplateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId_Code",
                table: "Permissions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberRules_TenantId_RuleCode",
                table: "NumberRules",
                columns: new[] { "TenantId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_TenantId_Code",
                table: "NotificationTemplates",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginFailureRecords_TenantId_UserName_IpAddress",
                table: "LoginFailureRecords",
                columns: new[] { "TenantId", "UserName", "IpAddress" },
                unique: true,
                filter: "[IpAddress] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IpAccessRules_TenantId_RuleType_IpPattern",
                table: "IpAccessRules",
                columns: new[] { "TenantId", "RuleType", "IpPattern" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryTypes_TenantId_Code",
                table: "DictionaryTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryItems_TenantId_TypeCode_Value",
                table: "DictionaryItems",
                columns: new[] { "TenantId", "TypeCode", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId_Code",
                table: "Departments",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_TenantId_OrderNo_IsDeleted",
                table: "demo_business_order",
                columns: new[] { "TenantId", "OrderNo", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_demo_approval_order_TenantId_OrderNo_IsDeleted",
                table: "demo_approval_order",
                columns: new[] { "TenantId", "OrderNo", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiClients_TenantId_ClientCode",
                table: "ApiClients",
                columns: new[] { "TenantId", "ClientCode" },
                unique: true);
        }
    }
}
