IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Menus] (
    [Id] uniqueidentifier NOT NULL,
    [ParentId] uniqueidentifier NULL,
    [Name] nvarchar(128) NOT NULL,
    [Path] nvarchar(256) NULL,
    [Component] nvarchar(256) NULL,
    [Redirect] nvarchar(256) NULL,
    [Icon] nvarchar(128) NULL,
    [Sort] int NOT NULL,
    [Visible] bit NOT NULL DEFAULT CAST(1 AS bit),
    [KeepAlive] bit NOT NULL DEFAULT CAST(0 AS bit),
    [MenuType] nvarchar(32) NOT NULL,
    [PermissionCode] nvarchar(128) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Menus] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Menus_Menus_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Menus] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [OperationLogs] (
    [Id] uniqueidentifier NOT NULL,
    [OperatorUserId] uniqueidentifier NULL,
    [Module] nvarchar(128) NOT NULL,
    [Action] nvarchar(128) NOT NULL,
    [RequestPath] nvarchar(512) NULL,
    [HttpMethod] nvarchar(16) NULL,
    [IpAddress] nvarchar(64) NULL,
    [UserAgent] nvarchar(512) NULL,
    [Succeeded] bit NOT NULL,
    [Message] nvarchar(1024) NULL,
    [OperatedAt] datetimeoffset NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_OperationLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [Permissions] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Group] nvarchar(128) NOT NULL,
    [Description] nvarchar(512) NULL,
    [Resource] nvarchar(128) NULL,
    [Action] nvarchar(64) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);

CREATE TABLE [Tenants] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(64) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Description] nvarchar(512) NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
);

CREATE TABLE [Departments] (
    [Id] uniqueidentifier NOT NULL,
    [ParentId] uniqueidentifier NULL,
    [Code] nvarchar(64) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Sort] int NOT NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Departments_Departments_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Departments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Roles] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(64) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Description] nvarchar(512) NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Roles_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [DepartmentId] uniqueidentifier NULL,
    [UserName] nvarchar(64) NOT NULL,
    [NormalizedUserName] nvarchar(64) NOT NULL,
    [Email] nvarchar(256) NULL,
    [PhoneNumber] nvarchar(32) NULL,
    [PasswordHash] nvarchar(512) NOT NULL,
    [DisplayName] nvarchar(128) NOT NULL,
    [AvatarUrl] nvarchar(512) NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [LastLoginAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RoleMenus] (
    [Id] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [MenuId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_RoleMenus] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoleMenus_Menus_MenuId] FOREIGN KEY ([MenuId]) REFERENCES [Menus] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RoleMenus_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [RolePermissions] (
    [Id] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [PermissionId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [UserRoles] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Departments_IsDeleted] ON [Departments] ([IsDeleted]);

CREATE INDEX [IX_Departments_ParentId] ON [Departments] ([ParentId]);

CREATE INDEX [IX_Departments_TenantId] ON [Departments] ([TenantId]);

CREATE UNIQUE INDEX [IX_Departments_TenantId_Code] ON [Departments] ([TenantId], [Code]);

CREATE INDEX [IX_Menus_IsDeleted] ON [Menus] ([IsDeleted]);

CREATE INDEX [IX_Menus_ParentId] ON [Menus] ([ParentId]);

CREATE INDEX [IX_Menus_TenantId] ON [Menus] ([TenantId]);

CREATE INDEX [IX_Menus_TenantId_PermissionCode] ON [Menus] ([TenantId], [PermissionCode]);

CREATE INDEX [IX_OperationLogs_IsDeleted] ON [OperationLogs] ([IsDeleted]);

CREATE INDEX [IX_OperationLogs_OperatorUserId] ON [OperationLogs] ([OperatorUserId]);

CREATE INDEX [IX_OperationLogs_TenantId] ON [OperationLogs] ([TenantId]);

CREATE INDEX [IX_OperationLogs_TenantId_OperatedAt] ON [OperationLogs] ([TenantId], [OperatedAt]);

CREATE INDEX [IX_Permissions_IsDeleted] ON [Permissions] ([IsDeleted]);

CREATE INDEX [IX_Permissions_TenantId] ON [Permissions] ([TenantId]);

CREATE UNIQUE INDEX [IX_Permissions_TenantId_Code] ON [Permissions] ([TenantId], [Code]);

CREATE INDEX [IX_RoleMenus_IsDeleted] ON [RoleMenus] ([IsDeleted]);

CREATE INDEX [IX_RoleMenus_MenuId] ON [RoleMenus] ([MenuId]);

CREATE INDEX [IX_RoleMenus_RoleId] ON [RoleMenus] ([RoleId]);

CREATE INDEX [IX_RoleMenus_TenantId] ON [RoleMenus] ([TenantId]);

CREATE UNIQUE INDEX [IX_RoleMenus_TenantId_RoleId_MenuId] ON [RoleMenus] ([TenantId], [RoleId], [MenuId]);

CREATE INDEX [IX_RolePermissions_IsDeleted] ON [RolePermissions] ([IsDeleted]);

CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);

CREATE INDEX [IX_RolePermissions_RoleId] ON [RolePermissions] ([RoleId]);

CREATE INDEX [IX_RolePermissions_TenantId] ON [RolePermissions] ([TenantId]);

CREATE UNIQUE INDEX [IX_RolePermissions_TenantId_RoleId_PermissionId] ON [RolePermissions] ([TenantId], [RoleId], [PermissionId]);

CREATE INDEX [IX_Roles_IsDeleted] ON [Roles] ([IsDeleted]);

CREATE INDEX [IX_Roles_TenantId] ON [Roles] ([TenantId]);

CREATE UNIQUE INDEX [IX_Roles_TenantId_Code] ON [Roles] ([TenantId], [Code]);

CREATE UNIQUE INDEX [IX_Tenants_Code] ON [Tenants] ([Code]);

CREATE INDEX [IX_Tenants_IsDeleted] ON [Tenants] ([IsDeleted]);

CREATE INDEX [IX_Tenants_TenantId] ON [Tenants] ([TenantId]);

CREATE INDEX [IX_UserRoles_IsDeleted] ON [UserRoles] ([IsDeleted]);

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);

CREATE INDEX [IX_UserRoles_TenantId] ON [UserRoles] ([TenantId]);

CREATE UNIQUE INDEX [IX_UserRoles_TenantId_UserId_RoleId] ON [UserRoles] ([TenantId], [UserId], [RoleId]);

CREATE INDEX [IX_UserRoles_UserId] ON [UserRoles] ([UserId]);

CREATE INDEX [IX_Users_DepartmentId] ON [Users] ([DepartmentId]);

CREATE INDEX [IX_Users_IsDeleted] ON [Users] ([IsDeleted]);

CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);

CREATE UNIQUE INDEX [IX_Users_TenantId_NormalizedUserName] ON [Users] ([TenantId], [NormalizedUserName]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260512060212_InitialCreate', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [OpenIddictApplications] (
    [Id] nvarchar(450) NOT NULL,
    [ApplicationType] nvarchar(50) NULL,
    [ClientId] nvarchar(100) NULL,
    [ClientSecret] nvarchar(max) NULL,
    [ClientType] nvarchar(50) NULL,
    [ConcurrencyToken] nvarchar(50) NULL,
    [ConsentType] nvarchar(50) NULL,
    [DisplayName] nvarchar(max) NULL,
    [DisplayNames] nvarchar(max) NULL,
    [JsonWebKeySet] nvarchar(max) NULL,
    [Permissions] nvarchar(max) NULL,
    [PostLogoutRedirectUris] nvarchar(max) NULL,
    [Properties] nvarchar(max) NULL,
    [RedirectUris] nvarchar(max) NULL,
    [Requirements] nvarchar(max) NULL,
    [Settings] nvarchar(max) NULL,
    CONSTRAINT [PK_OpenIddictApplications] PRIMARY KEY ([Id])
);

CREATE TABLE [OpenIddictScopes] (
    [Id] nvarchar(450) NOT NULL,
    [ConcurrencyToken] nvarchar(50) NULL,
    [Description] nvarchar(max) NULL,
    [Descriptions] nvarchar(max) NULL,
    [DisplayName] nvarchar(max) NULL,
    [DisplayNames] nvarchar(max) NULL,
    [Name] nvarchar(200) NULL,
    [Properties] nvarchar(max) NULL,
    [Resources] nvarchar(max) NULL,
    CONSTRAINT [PK_OpenIddictScopes] PRIMARY KEY ([Id])
);

CREATE TABLE [OpenIddictAuthorizations] (
    [Id] nvarchar(450) NOT NULL,
    [ApplicationId] nvarchar(450) NULL,
    [ConcurrencyToken] nvarchar(50) NULL,
    [CreationDate] datetime2 NULL,
    [Properties] nvarchar(max) NULL,
    [Scopes] nvarchar(max) NULL,
    [Status] nvarchar(50) NULL,
    [Subject] nvarchar(400) NULL,
    [Type] nvarchar(50) NULL,
    CONSTRAINT [PK_OpenIddictAuthorizations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OpenIddictAuthorizations_OpenIddictApplications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [OpenIddictApplications] ([Id])
);

CREATE TABLE [OpenIddictTokens] (
    [Id] nvarchar(450) NOT NULL,
    [ApplicationId] nvarchar(450) NULL,
    [AuthorizationId] nvarchar(450) NULL,
    [ConcurrencyToken] nvarchar(50) NULL,
    [CreationDate] datetime2 NULL,
    [ExpirationDate] datetime2 NULL,
    [Payload] nvarchar(max) NULL,
    [Properties] nvarchar(max) NULL,
    [RedemptionDate] datetime2 NULL,
    [ReferenceId] nvarchar(100) NULL,
    [Status] nvarchar(50) NULL,
    [Subject] nvarchar(400) NULL,
    [Type] nvarchar(150) NULL,
    CONSTRAINT [PK_OpenIddictTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OpenIddictTokens_OpenIddictApplications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [OpenIddictApplications] ([Id]),
    CONSTRAINT [FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId] FOREIGN KEY ([AuthorizationId]) REFERENCES [OpenIddictAuthorizations] ([Id])
);

CREATE UNIQUE INDEX [IX_OpenIddictApplications_ClientId] ON [OpenIddictApplications] ([ClientId]) WHERE [ClientId] IS NOT NULL;

CREATE INDEX [IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type] ON [OpenIddictAuthorizations] ([ApplicationId], [Status], [Subject], [Type]);

CREATE UNIQUE INDEX [IX_OpenIddictScopes_Name] ON [OpenIddictScopes] ([Name]) WHERE [Name] IS NOT NULL;

CREATE INDEX [IX_OpenIddictTokens_ApplicationId_Status_Subject_Type] ON [OpenIddictTokens] ([ApplicationId], [Status], [Subject], [Type]);

CREATE INDEX [IX_OpenIddictTokens_AuthorizationId] ON [OpenIddictTokens] ([AuthorizationId]);

CREATE UNIQUE INDEX [IX_OpenIddictTokens_ReferenceId] ON [OpenIddictTokens] ([ReferenceId]) WHERE [ReferenceId] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260512060935_AddOpenIddict', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ScheduledTasks] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [JobType] nvarchar(128) NOT NULL,
    [CronExpression] nvarchar(128) NOT NULL,
    [Queue] nvarchar(64) NOT NULL,
    [Description] nvarchar(512) NULL,
    [ParametersJson] nvarchar(4000) NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [LastRunAt] datetimeoffset NULL,
    [LastRunSucceeded] bit NULL,
    [LastRunMessage] nvarchar(1024) NULL,
    [LastJobId] nvarchar(128) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_ScheduledTasks] PRIMARY KEY ([Id])
);

CREATE TABLE [ScheduledTaskExecutionLogs] (
    [Id] uniqueidentifier NOT NULL,
    [ScheduledTaskId] uniqueidentifier NOT NULL,
    [JobId] nvarchar(128) NULL,
    [JobType] nvarchar(128) NOT NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [FinishedAt] datetimeoffset NULL,
    [Succeeded] bit NOT NULL,
    [Message] nvarchar(1024) NULL,
    [ParametersJson] nvarchar(4000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_ScheduledTaskExecutionLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ScheduledTaskExecutionLogs_ScheduledTasks_ScheduledTaskId] FOREIGN KEY ([ScheduledTaskId]) REFERENCES [ScheduledTasks] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ScheduledTaskExecutionLogs_IsDeleted] ON [ScheduledTaskExecutionLogs] ([IsDeleted]);

CREATE INDEX [IX_ScheduledTaskExecutionLogs_ScheduledTaskId] ON [ScheduledTaskExecutionLogs] ([ScheduledTaskId]);

CREATE INDEX [IX_ScheduledTaskExecutionLogs_TenantId] ON [ScheduledTaskExecutionLogs] ([TenantId]);

CREATE INDEX [IX_ScheduledTaskExecutionLogs_TenantId_ScheduledTaskId_StartedAt] ON [ScheduledTaskExecutionLogs] ([TenantId], [ScheduledTaskId], [StartedAt]);

CREATE INDEX [IX_ScheduledTasks_IsDeleted] ON [ScheduledTasks] ([IsDeleted]);

CREATE INDEX [IX_ScheduledTasks_TenantId] ON [ScheduledTasks] ([TenantId]);

CREATE UNIQUE INDEX [IX_ScheduledTasks_TenantId_Code] ON [ScheduledTasks] ([TenantId], [Code]) WHERE [TenantId] IS NOT NULL AND [Code] IS NOT NULL;

CREATE INDEX [IX_ScheduledTasks_TenantId_IsEnabled] ON [ScheduledTasks] ([TenantId], [IsEnabled]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260516000000_AddScheduledTasks', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_OperationLogs_TenantId_OperatedAt] ON [OperationLogs];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OperationLogs]') AND [c].[name] = N'HttpMethod');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [OperationLogs] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [OperationLogs] DROP COLUMN [HttpMethod];

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OperationLogs]') AND [c].[name] = N'Message');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [OperationLogs] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [OperationLogs] DROP COLUMN [Message];

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OperationLogs]') AND [c].[name] = N'OperatedAt');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [OperationLogs] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [OperationLogs] DROP COLUMN [OperatedAt];

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OperationLogs]') AND [c].[name] = N'Succeeded');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [OperationLogs] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [OperationLogs] DROP COLUMN [Succeeded];

EXEC sp_rename N'[OperationLogs].[OperatorUserId]', N'UserId', 'COLUMN';

EXEC sp_rename N'[OperationLogs].[IX_OperationLogs_OperatorUserId]', N'IX_OperationLogs_UserId', 'INDEX';

ALTER TABLE [OperationLogs] ADD [ElapsedMilliseconds] bigint NOT NULL DEFAULT CAST(0 AS bigint);

ALTER TABLE [OperationLogs] ADD [Method] nvarchar(128) NOT NULL DEFAULT N'';

ALTER TABLE [OperationLogs] ADD [RequestBody] nvarchar(4000) NULL;

ALTER TABLE [OperationLogs] ADD [RequestMethod] nvarchar(16) NOT NULL DEFAULT N'';

ALTER TABLE [OperationLogs] ADD [ResponseBody] nvarchar(4000) NULL;

ALTER TABLE [OperationLogs] ADD [StatusCode] int NOT NULL DEFAULT 0;

ALTER TABLE [OperationLogs] ADD [TraceId] nvarchar(128) NULL;

ALTER TABLE [OperationLogs] ADD [UserName] nvarchar(128) NULL;

CREATE TABLE [LoginLogs] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [UserName] nvarchar(128) NOT NULL,
    [LoginType] nvarchar(64) NOT NULL,
    [IpAddress] nvarchar(64) NULL,
    [UserAgent] nvarchar(512) NULL,
    [LoginResult] nvarchar(32) NOT NULL,
    [FailureReason] nvarchar(512) NULL,
    [TraceId] nvarchar(128) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_LoginLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_OperationLogs_TenantId_CreatedAt] ON [OperationLogs] ([TenantId], [CreatedAt]);

CREATE INDEX [IX_OperationLogs_TraceId] ON [OperationLogs] ([TraceId]);

CREATE INDEX [IX_LoginLogs_IsDeleted] ON [LoginLogs] ([IsDeleted]);

CREATE INDEX [IX_LoginLogs_TenantId] ON [LoginLogs] ([TenantId]);

CREATE INDEX [IX_LoginLogs_TenantId_CreatedAt] ON [LoginLogs] ([TenantId], [CreatedAt]);

CREATE INDEX [IX_LoginLogs_TraceId] ON [LoginLogs] ([TraceId]);

CREATE INDEX [IX_LoginLogs_UserId] ON [LoginLogs] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260516073249_AddOperationAndLoginLogs', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Departments] ADD [Status] nvarchar(32) NOT NULL DEFAULT N'Enabled';

ALTER TABLE [Departments] ADD [TreePath] nvarchar(1024) NOT NULL DEFAULT N'';

CREATE TABLE [RoleDataScopes] (
    [Id] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [ScopeType] int NOT NULL,
    [CustomDepartmentIds] nvarchar(2000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_RoleDataScopes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoleDataScopes_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [UserDataScopes] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ScopeType] int NOT NULL,
    [CustomDepartmentIds] nvarchar(2000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_UserDataScopes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserDataScopes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Departments_TenantId_ParentId] ON [Departments] ([TenantId], [ParentId]);

CREATE INDEX [IX_RoleDataScopes_IsDeleted] ON [RoleDataScopes] ([IsDeleted]);

CREATE UNIQUE INDEX [IX_RoleDataScopes_RoleId] ON [RoleDataScopes] ([RoleId]);

CREATE INDEX [IX_RoleDataScopes_TenantId] ON [RoleDataScopes] ([TenantId]);

CREATE UNIQUE INDEX [IX_RoleDataScopes_TenantId_RoleId] ON [RoleDataScopes] ([TenantId], [RoleId]);

CREATE INDEX [IX_UserDataScopes_IsDeleted] ON [UserDataScopes] ([IsDeleted]);

CREATE INDEX [IX_UserDataScopes_TenantId] ON [UserDataScopes] ([TenantId]);

CREATE UNIQUE INDEX [IX_UserDataScopes_TenantId_UserId] ON [UserDataScopes] ([TenantId], [UserId]);

CREATE UNIQUE INDEX [IX_UserDataScopes_UserId] ON [UserDataScopes] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260516081844_AddDataPermissionEngine', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [DictionaryItems] (
    [Id] uniqueidentifier NOT NULL,
    [TypeCode] nvarchar(64) NOT NULL,
    [Label] nvarchar(128) NOT NULL,
    [Value] nvarchar(128) NOT NULL,
    [Color] nvarchar(32) NULL,
    [CssClass] nvarchar(128) NULL,
    [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
    [Status] nvarchar(32) NOT NULL,
    [Sort] int NOT NULL,
    [Remark] nvarchar(512) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_DictionaryItems] PRIMARY KEY ([Id])
);

CREATE TABLE [DictionaryTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(64) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Description] nvarchar(512) NULL,
    [Status] nvarchar(32) NOT NULL,
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_DictionaryTypes] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_DictionaryItems_IsDeleted] ON [DictionaryItems] ([IsDeleted]);

CREATE INDEX [IX_DictionaryItems_TenantId] ON [DictionaryItems] ([TenantId]);

CREATE INDEX [IX_DictionaryItems_TenantId_TypeCode_Status] ON [DictionaryItems] ([TenantId], [TypeCode], [Status]);

CREATE UNIQUE INDEX [IX_DictionaryItems_TenantId_TypeCode_Value] ON [DictionaryItems] ([TenantId], [TypeCode], [Value]);

CREATE INDEX [IX_DictionaryTypes_IsDeleted] ON [DictionaryTypes] ([IsDeleted]);

CREATE INDEX [IX_DictionaryTypes_TenantId] ON [DictionaryTypes] ([TenantId]);

CREATE UNIQUE INDEX [IX_DictionaryTypes_TenantId_Code] ON [DictionaryTypes] ([TenantId], [Code]);

CREATE INDEX [IX_DictionaryTypes_TenantId_Status] ON [DictionaryTypes] ([TenantId], [Status]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260518010521_AddDictionaries', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [SystemConfigs] (
    [Id] uniqueidentifier NOT NULL,
    [ConfigKey] nvarchar(128) NOT NULL,
    [ConfigValue] nvarchar(4000) NOT NULL,
    [ConfigType] nvarchar(64) NOT NULL,
    [GroupCode] nvarchar(64) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Description] nvarchar(512) NULL,
    [IsEncrypted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [IsSystem] bit NOT NULL DEFAULT CAST(0 AS bit),
    [Status] nvarchar(32) NOT NULL,
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_SystemConfigs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_SystemConfigs_IsDeleted] ON [SystemConfigs] ([IsDeleted]);

CREATE INDEX [IX_SystemConfigs_TenantId] ON [SystemConfigs] ([TenantId]);

CREATE UNIQUE INDEX [IX_SystemConfigs_TenantId_ConfigKey] ON [SystemConfigs] ([TenantId], [ConfigKey]);

CREATE INDEX [IX_SystemConfigs_TenantId_GroupCode_Status] ON [SystemConfigs] ([TenantId], [GroupCode], [Status]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260518011404_AddSystemConfigs', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [FileResources] (
    [Id] uniqueidentifier NOT NULL,
    [OriginalName] nvarchar(255) NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [Extension] nvarchar(32) NOT NULL,
    [ContentType] nvarchar(128) NOT NULL,
    [Size] bigint NOT NULL,
    [StorageProvider] nvarchar(32) NOT NULL,
    [BucketName] nvarchar(128) NOT NULL,
    [ObjectKey] nvarchar(512) NOT NULL,
    [Url] nvarchar(1024) NULL,
    [Md5] nvarchar(32) NOT NULL,
    [BusinessType] nvarchar(128) NULL,
    [BusinessId] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_FileResources] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_FileResources_IsDeleted] ON [FileResources] ([IsDeleted]);

CREATE INDEX [IX_FileResources_TenantId] ON [FileResources] ([TenantId]);

CREATE INDEX [IX_FileResources_TenantId_BusinessType_BusinessId] ON [FileResources] ([TenantId], [BusinessType], [BusinessId]);

CREATE INDEX [IX_FileResources_TenantId_CreatedAt] ON [FileResources] ([TenantId], [CreatedAt]);

CREATE INDEX [IX_FileResources_TenantId_Md5] ON [FileResources] ([TenantId], [Md5]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260518012417_AddFileResources', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [InboxMessages] (
    [Id] uniqueidentifier NOT NULL,
    [MessageId] nvarchar(64) NOT NULL,
    [Consumer] nvarchar(128) NOT NULL,
    [MessageType] nvarchar(256) NOT NULL,
    [PayloadHash] nvarchar(64) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [ProcessedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_InboxMessages] PRIMARY KEY ([Id])
);

CREATE TABLE [OutboxMessages] (
    [Id] uniqueidentifier NOT NULL,
    [MessageId] nvarchar(64) NOT NULL,
    [Exchange] nvarchar(128) NOT NULL,
    [RoutingKey] nvarchar(256) NOT NULL,
    [MessageType] nvarchar(256) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Headers] nvarchar(max) NULL,
    [Status] nvarchar(32) NOT NULL,
    [RetryCount] int NOT NULL,
    [NextRetryAt] datetimeoffset NULL,
    [ErrorMessage] nvarchar(2000) NULL,
    [ProcessedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_InboxMessages_IsDeleted] ON [InboxMessages] ([IsDeleted]);

CREATE INDEX [IX_InboxMessages_TenantId] ON [InboxMessages] ([TenantId]);

CREATE INDEX [IX_InboxMessages_TenantId_Consumer_Status_CreatedAt] ON [InboxMessages] ([TenantId], [Consumer], [Status], [CreatedAt]);

CREATE UNIQUE INDEX [IX_InboxMessages_TenantId_MessageId_Consumer] ON [InboxMessages] ([TenantId], [MessageId], [Consumer]);

CREATE INDEX [IX_OutboxMessages_IsDeleted] ON [OutboxMessages] ([IsDeleted]);

CREATE INDEX [IX_OutboxMessages_TenantId] ON [OutboxMessages] ([TenantId]);

CREATE UNIQUE INDEX [IX_OutboxMessages_TenantId_MessageId] ON [OutboxMessages] ([TenantId], [MessageId]);

CREATE INDEX [IX_OutboxMessages_TenantId_MessageType_CreatedAt] ON [OutboxMessages] ([TenantId], [MessageType], [CreatedAt]);

CREATE INDEX [IX_OutboxMessages_TenantId_Status_NextRetryAt] ON [OutboxMessages] ([TenantId], [Status], [NextRetryAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260518020007_AddReliableMessages', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [ScheduledTaskExecutionLogs] ADD [TraceId] nvarchar(128) NULL;

CREATE INDEX [IX_ScheduledTaskExecutionLogs_TraceId] ON [ScheduledTaskExecutionLogs] ([TraceId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260519011114_AddScheduledTaskExecutionLogTraceId', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [JobExecutionLogs] (
    [Id] uniqueidentifier NOT NULL,
    [JobName] nvarchar(200) NOT NULL,
    [JobId] nvarchar(128) NULL,
    [Status] nvarchar(32) NOT NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [FinishedAt] datetimeoffset NULL,
    [ElapsedMilliseconds] bigint NOT NULL,
    [ErrorMessage] nvarchar(2000) NULL,
    [TraceId] nvarchar(128) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_JobExecutionLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_JobExecutionLogs_IsDeleted] ON [JobExecutionLogs] ([IsDeleted]);

CREATE INDEX [IX_JobExecutionLogs_JobId] ON [JobExecutionLogs] ([JobId]);

CREATE INDEX [IX_JobExecutionLogs_TenantId] ON [JobExecutionLogs] ([TenantId]);

CREATE INDEX [IX_JobExecutionLogs_TenantId_JobName_StartedAt] ON [JobExecutionLogs] ([TenantId], [JobName], [StartedAt]);

CREATE INDEX [IX_JobExecutionLogs_TraceId] ON [JobExecutionLogs] ([TraceId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260519012932_AddJobExecutionLogs', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Notifications] (
    [Id] uniqueidentifier NOT NULL,
    [Type] nvarchar(32) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Content] nvarchar(4000) NOT NULL,
    [SenderId] uniqueidentifier NULL,
    [SenderName] nvarchar(100) NULL,
    [LinkUrl] nvarchar(500) NULL,
    [Payload] nvarchar(4000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Type] nvarchar(32) NOT NULL,
    [TitleTemplate] nvarchar(200) NOT NULL,
    [ContentTemplate] nvarchar(4000) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [Sort] int NOT NULL,
    [Remark] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_NotificationTemplates] PRIMARY KEY ([Id])
);

CREATE TABLE [UserNotifications] (
    [Id] uniqueidentifier NOT NULL,
    [NotificationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [IsRead] bit NOT NULL,
    [ReadAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserNotifications_Notifications_NotificationId] FOREIGN KEY ([NotificationId]) REFERENCES [Notifications] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Notifications_IsDeleted] ON [Notifications] ([IsDeleted]);

CREATE INDEX [IX_Notifications_TenantId] ON [Notifications] ([TenantId]);

CREATE INDEX [IX_Notifications_TenantId_Type_CreatedAt] ON [Notifications] ([TenantId], [Type], [CreatedAt]);

CREATE INDEX [IX_NotificationTemplates_IsDeleted] ON [NotificationTemplates] ([IsDeleted]);

CREATE INDEX [IX_NotificationTemplates_TenantId] ON [NotificationTemplates] ([TenantId]);

CREATE UNIQUE INDEX [IX_NotificationTemplates_TenantId_Code] ON [NotificationTemplates] ([TenantId], [Code]);

CREATE INDEX [IX_UserNotifications_IsDeleted] ON [UserNotifications] ([IsDeleted]);

CREATE INDEX [IX_UserNotifications_NotificationId] ON [UserNotifications] ([NotificationId]);

CREATE INDEX [IX_UserNotifications_TenantId] ON [UserNotifications] ([TenantId]);

CREATE INDEX [IX_UserNotifications_TenantId_UserId_IsRead_CreatedAt] ON [UserNotifications] ([TenantId], [UserId], [IsRead], [CreatedAt]);

CREATE UNIQUE INDEX [IX_UserNotifications_UserId_NotificationId] ON [UserNotifications] ([UserId], [NotificationId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260519014222_AddNotifications', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [UserSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [UserName] nvarchar(128) NOT NULL,
    [SessionId] nvarchar(128) NOT NULL,
    [AccessTokenId] nvarchar(128) NULL,
    [RefreshTokenId] nvarchar(128) NULL,
    [IpAddress] nvarchar(64) NULL,
    [UserAgent] nvarchar(512) NULL,
    [LoginAt] datetimeoffset NOT NULL,
    [LastActiveAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [IsRevoked] bit NOT NULL,
    [RevokedAt] datetimeoffset NULL,
    [RevokedReason] nvarchar(512) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_UserSessions] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_UserSessions_IsDeleted] ON [UserSessions] ([IsDeleted]);

CREATE INDEX [IX_UserSessions_RefreshTokenId] ON [UserSessions] ([RefreshTokenId]);

CREATE UNIQUE INDEX [IX_UserSessions_SessionId] ON [UserSessions] ([SessionId]);

CREATE INDEX [IX_UserSessions_TenantId] ON [UserSessions] ([TenantId]);

CREATE INDEX [IX_UserSessions_TenantId_UserId_IsRevoked_LastActiveAt] ON [UserSessions] ([TenantId], [UserId], [IsRevoked], [LastActiveAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260519014928_AddUserSessions', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Users] ADD [IsBuiltin] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Roles] ADD [IsBuiltin] bit NOT NULL DEFAULT CAST(0 AS bit);

UPDATE Users SET IsBuiltin = 1 WHERE NormalizedUserName = 'ADMIN';

UPDATE Roles SET IsBuiltin = 1 WHERE Code = 'SuperAdmin';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260525010100_AddBuiltinProtectionFlags', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [wf_definition] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Version] int NOT NULL DEFAULT 1,
    [Status] int NOT NULL DEFAULT 0,
    [IsPublished] bit NOT NULL DEFAULT CAST(0 AS bit),
    [PublishedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_definition] PRIMARY KEY ([Id])
);

CREATE TABLE [wf_business_binding] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [DefinitionId] uniqueidentifier NOT NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_business_binding] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_wf_business_binding_wf_definition_DefinitionId] FOREIGN KEY ([DefinitionId]) REFERENCES [wf_definition] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [wf_condition] (
    [Id] uniqueidentifier NOT NULL,
    [DefinitionId] uniqueidentifier NOT NULL,
    [NodeKey] nvarchar(100) NOT NULL,
    [ConditionName] nvarchar(200) NOT NULL,
    [ExpressionJson] nvarchar(max) NOT NULL,
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_condition] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_wf_condition_wf_definition_DefinitionId] FOREIGN KEY ([DefinitionId]) REFERENCES [wf_definition] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [wf_instance] (
    [Id] uniqueidentifier NOT NULL,
    [DefinitionId] uniqueidentifier NOT NULL,
    [DefinitionCode] nvarchar(100) NOT NULL,
    [DefinitionName] nvarchar(200) NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [BusinessId] nvarchar(100) NOT NULL,
    [BusinessTitle] nvarchar(300) NOT NULL,
    [StarterUserId] uniqueidentifier NOT NULL,
    [StarterUserName] nvarchar(100) NOT NULL,
    [Status] int NOT NULL DEFAULT 0,
    [CurrentNodeKey] nvarchar(100) NULL,
    [FormDataJson] nvarchar(max) NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_instance] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_wf_instance_wf_definition_DefinitionId] FOREIGN KEY ([DefinitionId]) REFERENCES [wf_definition] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [wf_node] (
    [Id] uniqueidentifier NOT NULL,
    [DefinitionId] uniqueidentifier NOT NULL,
    [NodeKey] nvarchar(100) NOT NULL,
    [NodeName] nvarchar(200) NOT NULL,
    [NodeType] int NOT NULL,
    [ApproverType] int NULL,
    [ApproverIds] nvarchar(2000) NULL,
    [ApprovalMode] int NULL,
    [ConfigJson] nvarchar(max) NULL,
    [PositionX] decimal(18,2) NOT NULL,
    [PositionY] decimal(18,2) NOT NULL,
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_node] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_wf_node_wf_definition_DefinitionId] FOREIGN KEY ([DefinitionId]) REFERENCES [wf_definition] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [wf_edge] (
    [Id] uniqueidentifier NOT NULL,
    [DefinitionId] uniqueidentifier NOT NULL,
    [FromNodeKey] nvarchar(100) NOT NULL,
    [ToNodeKey] nvarchar(100) NOT NULL,
    [ConditionId] uniqueidentifier NULL,
    [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_edge] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_wf_edge_wf_condition_ConditionId] FOREIGN KEY ([ConditionId]) REFERENCES [wf_condition] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_wf_edge_wf_definition_DefinitionId] FOREIGN KEY ([DefinitionId]) REFERENCES [wf_definition] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [wf_cc] (
    [Id] uniqueidentifier NOT NULL,
    [InstanceId] uniqueidentifier NOT NULL,
    [NodeKey] nvarchar(100) NOT NULL,
    [CcUserId] uniqueidentifier NOT NULL,
    [CcUserName] nvarchar(100) NOT NULL,
    [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
    [ReadAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_cc] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_wf_cc_wf_instance_InstanceId] FOREIGN KEY ([InstanceId]) REFERENCES [wf_instance] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [wf_task] (
    [Id] uniqueidentifier NOT NULL,
    [InstanceId] uniqueidentifier NOT NULL,
    [NodeKey] nvarchar(100) NOT NULL,
    [NodeName] nvarchar(200) NOT NULL,
    [ApproverUserId] uniqueidentifier NOT NULL,
    [ApproverUserName] nvarchar(100) NOT NULL,
    [Status] int NOT NULL DEFAULT 0,
    [AssignedAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NULL,
    [DueAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_task] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_wf_task_wf_instance_InstanceId] FOREIGN KEY ([InstanceId]) REFERENCES [wf_instance] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [wf_record] (
    [Id] uniqueidentifier NOT NULL,
    [InstanceId] uniqueidentifier NOT NULL,
    [TaskId] uniqueidentifier NULL,
    [NodeKey] nvarchar(100) NULL,
    [NodeName] nvarchar(200) NULL,
    [OperatorUserId] uniqueidentifier NULL,
    [OperatorUserName] nvarchar(100) NULL,
    [Action] int NOT NULL,
    [Comment] nvarchar(1000) NULL,
    [OperatedAt] datetimeoffset NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_wf_record] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_wf_record_wf_instance_InstanceId] FOREIGN KEY ([InstanceId]) REFERENCES [wf_instance] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_wf_record_wf_task_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [wf_task] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_wf_business_binding_DefinitionId] ON [wf_business_binding] ([DefinitionId]);

CREATE INDEX [IX_wf_business_binding_IsDeleted] ON [wf_business_binding] ([IsDeleted]);

CREATE INDEX [IX_wf_business_binding_TenantId] ON [wf_business_binding] ([TenantId]);

CREATE UNIQUE INDEX [IX_wf_business_binding_TenantId_BusinessType] ON [wf_business_binding] ([TenantId], [BusinessType]);

CREATE INDEX [IX_wf_business_binding_TenantId_DefinitionId_IsEnabled] ON [wf_business_binding] ([TenantId], [DefinitionId], [IsEnabled]);

CREATE INDEX [IX_wf_cc_InstanceId] ON [wf_cc] ([InstanceId]);

CREATE INDEX [IX_wf_cc_IsDeleted] ON [wf_cc] ([IsDeleted]);

CREATE INDEX [IX_wf_cc_TenantId] ON [wf_cc] ([TenantId]);

CREATE INDEX [IX_wf_cc_TenantId_CcUserId_IsRead_CreatedAt] ON [wf_cc] ([TenantId], [CcUserId], [IsRead], [CreatedAt]);

CREATE INDEX [IX_wf_cc_TenantId_InstanceId_CcUserId] ON [wf_cc] ([TenantId], [InstanceId], [CcUserId]);

CREATE INDEX [IX_wf_condition_DefinitionId] ON [wf_condition] ([DefinitionId]);

CREATE INDEX [IX_wf_condition_IsDeleted] ON [wf_condition] ([IsDeleted]);

CREATE INDEX [IX_wf_condition_TenantId] ON [wf_condition] ([TenantId]);

CREATE INDEX [IX_wf_condition_TenantId_DefinitionId_NodeKey_Sort] ON [wf_condition] ([TenantId], [DefinitionId], [NodeKey], [Sort]);

CREATE INDEX [IX_wf_definition_IsDeleted] ON [wf_definition] ([IsDeleted]);

CREATE INDEX [IX_wf_definition_TenantId] ON [wf_definition] ([TenantId]);

CREATE UNIQUE INDEX [IX_wf_definition_TenantId_Code_Version] ON [wf_definition] ([TenantId], [Code], [Version]);

CREATE INDEX [IX_wf_definition_TenantId_Status_IsPublished] ON [wf_definition] ([TenantId], [Status], [IsPublished]);

CREATE INDEX [IX_wf_edge_ConditionId] ON [wf_edge] ([ConditionId]);

CREATE INDEX [IX_wf_edge_DefinitionId] ON [wf_edge] ([DefinitionId]);

CREATE INDEX [IX_wf_edge_IsDeleted] ON [wf_edge] ([IsDeleted]);

CREATE INDEX [IX_wf_edge_TenantId] ON [wf_edge] ([TenantId]);

CREATE INDEX [IX_wf_edge_TenantId_DefinitionId_FromNodeKey] ON [wf_edge] ([TenantId], [DefinitionId], [FromNodeKey]);

CREATE INDEX [IX_wf_edge_TenantId_DefinitionId_Sort] ON [wf_edge] ([TenantId], [DefinitionId], [Sort]);

CREATE INDEX [IX_wf_edge_TenantId_DefinitionId_ToNodeKey] ON [wf_edge] ([TenantId], [DefinitionId], [ToNodeKey]);

CREATE INDEX [IX_wf_instance_DefinitionId] ON [wf_instance] ([DefinitionId]);

CREATE INDEX [IX_wf_instance_IsDeleted] ON [wf_instance] ([IsDeleted]);

CREATE INDEX [IX_wf_instance_TenantId] ON [wf_instance] ([TenantId]);

CREATE INDEX [IX_wf_instance_TenantId_BusinessType_BusinessId] ON [wf_instance] ([TenantId], [BusinessType], [BusinessId]);

CREATE INDEX [IX_wf_instance_TenantId_StarterUserId_Status_CreatedAt] ON [wf_instance] ([TenantId], [StarterUserId], [Status], [CreatedAt]);

CREATE INDEX [IX_wf_instance_TenantId_Status_CreatedAt] ON [wf_instance] ([TenantId], [Status], [CreatedAt]);

CREATE INDEX [IX_wf_node_DefinitionId] ON [wf_node] ([DefinitionId]);

CREATE INDEX [IX_wf_node_IsDeleted] ON [wf_node] ([IsDeleted]);

CREATE INDEX [IX_wf_node_TenantId] ON [wf_node] ([TenantId]);

CREATE UNIQUE INDEX [IX_wf_node_TenantId_DefinitionId_NodeKey] ON [wf_node] ([TenantId], [DefinitionId], [NodeKey]);

CREATE INDEX [IX_wf_node_TenantId_DefinitionId_NodeType] ON [wf_node] ([TenantId], [DefinitionId], [NodeType]);

CREATE INDEX [IX_wf_record_InstanceId] ON [wf_record] ([InstanceId]);

CREATE INDEX [IX_wf_record_IsDeleted] ON [wf_record] ([IsDeleted]);

CREATE INDEX [IX_wf_record_TaskId] ON [wf_record] ([TaskId]);

CREATE INDEX [IX_wf_record_TenantId] ON [wf_record] ([TenantId]);

CREATE INDEX [IX_wf_record_TenantId_InstanceId_OperatedAt] ON [wf_record] ([TenantId], [InstanceId], [OperatedAt]);

CREATE INDEX [IX_wf_record_TenantId_OperatorUserId_OperatedAt] ON [wf_record] ([TenantId], [OperatorUserId], [OperatedAt]);

CREATE INDEX [IX_wf_task_InstanceId] ON [wf_task] ([InstanceId]);

CREATE INDEX [IX_wf_task_IsDeleted] ON [wf_task] ([IsDeleted]);

CREATE INDEX [IX_wf_task_TenantId] ON [wf_task] ([TenantId]);

CREATE INDEX [IX_wf_task_TenantId_ApproverUserId_Status_CreatedAt] ON [wf_task] ([TenantId], [ApproverUserId], [Status], [CreatedAt]);

CREATE INDEX [IX_wf_task_TenantId_InstanceId_NodeKey] ON [wf_task] ([TenantId], [InstanceId], [NodeKey]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260525072017_AddWorkflowEntities', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_wf_business_binding_TenantId_BusinessType] ON [wf_business_binding];

CREATE UNIQUE INDEX [IX_wf_business_binding_TenantId_BusinessType_IsDeleted] ON [wf_business_binding] ([TenantId], [BusinessType], [IsDeleted]) WHERE [TenantId] IS NOT NULL AND [BusinessType] IS NOT NULL AND [IsDeleted] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260525093000_AdjustWorkflowBusinessBindingIndex', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [wf_business_binding] ADD [BusinessName] nvarchar(200) NOT NULL DEFAULT N'';

ALTER TABLE [wf_business_binding] ADD [DefinitionCode] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [wf_business_binding] ADD [DefinitionName] nvarchar(200) NOT NULL DEFAULT N'';

ALTER TABLE [wf_business_binding] ADD [Remark] nvarchar(1000) NULL;

CREATE TABLE [demo_approval_order] (
    [Id] uniqueidentifier NOT NULL,
    [OrderNo] nvarchar(100) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [DepartmentId] uniqueidentifier NULL,
    [ApplicantUserId] uniqueidentifier NOT NULL,
    [ApplicantUserName] nvarchar(100) NOT NULL,
    [ApprovalStatus] int NOT NULL DEFAULT 0,
    [WorkflowInstanceId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    [SubmittedBy] uniqueidentifier NULL,
    [ApprovedAt] datetimeoffset NULL,
    [RejectedAt] datetimeoffset NULL,
    [WithdrawnAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_demo_approval_order] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_demo_approval_order_IsDeleted] ON [demo_approval_order] ([IsDeleted]);

CREATE INDEX [IX_demo_approval_order_TenantId] ON [demo_approval_order] ([TenantId]);

CREATE INDEX [IX_demo_approval_order_TenantId_ApprovalStatus_CreatedAt] ON [demo_approval_order] ([TenantId], [ApprovalStatus], [CreatedAt]);

CREATE UNIQUE INDEX [IX_demo_approval_order_TenantId_OrderNo_IsDeleted] ON [demo_approval_order] ([TenantId], [OrderNo], [IsDeleted]) WHERE [TenantId] IS NOT NULL AND [OrderNo] IS NOT NULL AND [IsDeleted] IS NOT NULL;

CREATE INDEX [IX_demo_approval_order_TenantId_WorkflowInstanceId] ON [demo_approval_order] ([TenantId], [WorkflowInstanceId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260526090000_AddWorkflowBusinessAccess', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [NumberRules] (
    [Id] uniqueidentifier NOT NULL,
    [RuleCode] nvarchar(100) NOT NULL,
    [RuleName] nvarchar(200) NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [Prefix] nvarchar(50) NOT NULL,
    [DateFormat] nvarchar(32) NOT NULL,
    [SequenceLength] int NOT NULL DEFAULT 4,
    [ResetCycle] int NOT NULL DEFAULT 1,
    [Separator] nvarchar(16) NOT NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [Remark] nvarchar(512) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_NumberRules] PRIMARY KEY ([Id])
);

CREATE TABLE [NumberRuleSegments] (
    [Id] uniqueidentifier NOT NULL,
    [RuleId] uniqueidentifier NOT NULL,
    [SegmentType] int NOT NULL,
    [SegmentValue] nvarchar(256) NOT NULL,
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_NumberRuleSegments] PRIMARY KEY ([Id])
);

CREATE TABLE [NumberSequences] (
    [Id] uniqueidentifier NOT NULL,
    [RuleCode] nvarchar(100) NOT NULL,
    [SequenceKey] nvarchar(160) NOT NULL,
    [CurrentValue] bigint NOT NULL,
    [LastGeneratedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_NumberSequences] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_NumberRules_IsDeleted] ON [NumberRules] ([IsDeleted]);

CREATE INDEX [IX_NumberRules_TenantId] ON [NumberRules] ([TenantId]);

CREATE INDEX [IX_NumberRules_TenantId_BusinessType_IsEnabled] ON [NumberRules] ([TenantId], [BusinessType], [IsEnabled]);

CREATE UNIQUE INDEX [IX_NumberRules_TenantId_RuleCode] ON [NumberRules] ([TenantId], [RuleCode]) WHERE [TenantId] IS NOT NULL AND [RuleCode] IS NOT NULL;

CREATE INDEX [IX_NumberRuleSegments_IsDeleted] ON [NumberRuleSegments] ([IsDeleted]);

CREATE INDEX [IX_NumberRuleSegments_TenantId] ON [NumberRuleSegments] ([TenantId]);

CREATE INDEX [IX_NumberRuleSegments_TenantId_RuleId_Sort] ON [NumberRuleSegments] ([TenantId], [RuleId], [Sort]);

CREATE INDEX [IX_NumberSequences_IsDeleted] ON [NumberSequences] ([IsDeleted]);

CREATE INDEX [IX_NumberSequences_TenantId] ON [NumberSequences] ([TenantId]);

CREATE UNIQUE INDEX [IX_NumberSequences_TenantId_RuleCode_SequenceKey] ON [NumberSequences] ([TenantId], [RuleCode], [SequenceKey]) WHERE [TenantId] IS NOT NULL AND [RuleCode] IS NOT NULL AND [SequenceKey] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260526143000_AddNumberRuleEngine', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StateMachineDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_StateMachineDefinitions] PRIMARY KEY ([Id])
);

CREATE TABLE [StateDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [MachineId] uniqueidentifier NOT NULL,
    [StateCode] nvarchar(100) NOT NULL,
    [StateName] nvarchar(200) NOT NULL,
    [StateType] nvarchar(64) NOT NULL,
    [Color] nvarchar(32) NULL,
    [Sort] int NOT NULL,
    [IsInitial] bit NOT NULL DEFAULT CAST(0 AS bit),
    [IsFinal] bit NOT NULL DEFAULT CAST(0 AS bit),
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_StateDefinitions] PRIMARY KEY ([Id])
);

CREATE TABLE [StateTransitions] (
    [Id] uniqueidentifier NOT NULL,
    [MachineId] uniqueidentifier NOT NULL,
    [FromState] nvarchar(100) NOT NULL,
    [ToState] nvarchar(100) NOT NULL,
    [ActionCode] nvarchar(100) NOT NULL,
    [ActionName] nvarchar(200) NOT NULL,
    [RequiredPermission] nvarchar(200) NULL,
    [ConditionJson] nvarchar(4000) NULL,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_StateTransitions] PRIMARY KEY ([Id])
);

CREATE TABLE [StateTransitionLogs] (
    [Id] uniqueidentifier NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [BusinessId] nvarchar(100) NOT NULL,
    [FromState] nvarchar(100) NOT NULL,
    [ToState] nvarchar(100) NOT NULL,
    [ActionCode] nvarchar(100) NOT NULL,
    [ActionName] nvarchar(200) NOT NULL,
    [OperatorUserId] uniqueidentifier NULL,
    [OperatorUserName] nvarchar(100) NULL,
    [Comment] nvarchar(1000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_StateTransitionLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_StateMachineDefinitions_IsDeleted] ON [StateMachineDefinitions] ([IsDeleted]);

CREATE INDEX [IX_StateMachineDefinitions_TenantId] ON [StateMachineDefinitions] ([TenantId]);

CREATE UNIQUE INDEX [IX_StateMachineDefinitions_TenantId_BusinessType] ON [StateMachineDefinitions] ([TenantId], [BusinessType]) WHERE [TenantId] IS NOT NULL AND [BusinessType] IS NOT NULL;

CREATE INDEX [IX_StateMachineDefinitions_TenantId_IsEnabled] ON [StateMachineDefinitions] ([TenantId], [IsEnabled]);

CREATE INDEX [IX_StateDefinitions_IsDeleted] ON [StateDefinitions] ([IsDeleted]);

CREATE INDEX [IX_StateDefinitions_TenantId] ON [StateDefinitions] ([TenantId]);

CREATE INDEX [IX_StateDefinitions_TenantId_MachineId_Sort] ON [StateDefinitions] ([TenantId], [MachineId], [Sort]);

CREATE UNIQUE INDEX [IX_StateDefinitions_TenantId_MachineId_StateCode] ON [StateDefinitions] ([TenantId], [MachineId], [StateCode]) WHERE [TenantId] IS NOT NULL AND [MachineId] IS NOT NULL AND [StateCode] IS NOT NULL;

CREATE INDEX [IX_StateTransitions_IsDeleted] ON [StateTransitions] ([IsDeleted]);

CREATE INDEX [IX_StateTransitions_TenantId] ON [StateTransitions] ([TenantId]);

CREATE INDEX [IX_StateTransitions_TenantId_MachineId_FromState_ActionCode] ON [StateTransitions] ([TenantId], [MachineId], [FromState], [ActionCode]);

CREATE INDEX [IX_StateTransitions_TenantId_MachineId_IsEnabled_Sort] ON [StateTransitions] ([TenantId], [MachineId], [IsEnabled], [Sort]);

CREATE INDEX [IX_StateTransitionLogs_IsDeleted] ON [StateTransitionLogs] ([IsDeleted]);

CREATE INDEX [IX_StateTransitionLogs_TenantId] ON [StateTransitionLogs] ([TenantId]);

CREATE INDEX [IX_StateTransitionLogs_TenantId_BusinessType_ActionCode_CreatedAt] ON [StateTransitionLogs] ([TenantId], [BusinessType], [ActionCode], [CreatedAt]);

CREATE INDEX [IX_StateTransitionLogs_TenantId_BusinessType_BusinessId_CreatedAt] ON [StateTransitionLogs] ([TenantId], [BusinessType], [BusinessId], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260526150000_AddStateMachineEngine', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [PrintTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [TemplateCode] nvarchar(100) NOT NULL,
    [TemplateName] nvarchar(200) NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [TemplateType] nvarchar(64) NOT NULL,
    [ContentHtml] nvarchar(max) NOT NULL,
    [ContentJson] nvarchar(max) NULL,
    [PaperSize] nvarchar(32) NOT NULL,
    [Orientation] nvarchar(32) NOT NULL,
    [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [Version] int NOT NULL DEFAULT 1,
    [Remark] nvarchar(512) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_PrintTemplates] PRIMARY KEY ([Id])
);

CREATE TABLE [PrintRecords] (
    [Id] uniqueidentifier NOT NULL,
    [TemplateId] uniqueidentifier NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [BusinessId] nvarchar(100) NOT NULL,
    [PrintUserId] uniqueidentifier NULL,
    [PrintUserName] nvarchar(100) NULL,
    [PrintedAt] datetimeoffset NOT NULL,
    [PrintCount] int NOT NULL DEFAULT 1,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_PrintRecords] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_PrintTemplates_IsDeleted] ON [PrintTemplates] ([IsDeleted]);

CREATE INDEX [IX_PrintTemplates_TenantId] ON [PrintTemplates] ([TenantId]);

CREATE INDEX [IX_PrintTemplates_TenantId_BusinessType_IsDefault] ON [PrintTemplates] ([TenantId], [BusinessType], [IsDefault]);

CREATE INDEX [IX_PrintTemplates_TenantId_BusinessType_TemplateType_IsEnabled] ON [PrintTemplates] ([TenantId], [BusinessType], [TemplateType], [IsEnabled]);

CREATE UNIQUE INDEX [IX_PrintTemplates_TenantId_TemplateCode] ON [PrintTemplates] ([TenantId], [TemplateCode]) WHERE [TenantId] IS NOT NULL AND [TemplateCode] IS NOT NULL;

CREATE INDEX [IX_PrintRecords_IsDeleted] ON [PrintRecords] ([IsDeleted]);

CREATE INDEX [IX_PrintRecords_TenantId] ON [PrintRecords] ([TenantId]);

CREATE INDEX [IX_PrintRecords_TenantId_BusinessType_BusinessId_PrintedAt] ON [PrintRecords] ([TenantId], [BusinessType], [BusinessId], [PrintedAt]);

CREATE INDEX [IX_PrintRecords_TenantId_TemplateId_PrintedAt] ON [PrintRecords] ([TenantId], [TemplateId], [PrintedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260526153000_AddPrintTemplateEngine', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ReportDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [ReportCode] nvarchar(100) NOT NULL,
    [ReportName] nvarchar(200) NOT NULL,
    [Category] nvarchar(100) NOT NULL,
    [DataSourceType] nvarchar(50) NOT NULL,
    [SqlText] nvarchar(max) NULL,
    [ApiUrl] nvarchar(500) NULL,
    [ColumnsJson] nvarchar(max) NULL,
    [ParamsJson] nvarchar(max) NULL,
    [IsEnabled] bit NOT NULL,
    [Remark] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_ReportDefinitions] PRIMARY KEY ([Id])
);

CREATE TABLE [ReportQueryParams] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [ParamCode] nvarchar(100) NOT NULL,
    [ParamName] nvarchar(200) NOT NULL,
    [ParamType] nvarchar(50) NOT NULL,
    [DefaultValue] nvarchar(500) NULL,
    [Required] bit NOT NULL,
    [Sort] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_ReportQueryParams] PRIMARY KEY ([Id])
);

CREATE TABLE [ReportExecutionLogs] (
    [Id] uniqueidentifier NOT NULL,
    [ReportId] uniqueidentifier NOT NULL,
    [ReportCode] nvarchar(100) NOT NULL,
    [ExecuteUserId] uniqueidentifier NULL,
    [ExecuteUserName] nvarchar(100) NULL,
    [ParamsJson] nvarchar(max) NULL,
    [ElapsedMilliseconds] bigint NOT NULL,
    [RowCount] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_ReportExecutionLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_ReportDefinitions_IsDeleted] ON [ReportDefinitions] ([IsDeleted]);

CREATE INDEX [IX_ReportDefinitions_TenantId] ON [ReportDefinitions] ([TenantId]);

CREATE INDEX [IX_ReportDefinitions_TenantId_Category_IsEnabled] ON [ReportDefinitions] ([TenantId], [Category], [IsEnabled]);

CREATE UNIQUE INDEX [IX_ReportDefinitions_TenantId_ReportCode] ON [ReportDefinitions] ([TenantId], [ReportCode]) WHERE [TenantId] IS NOT NULL AND [ReportCode] IS NOT NULL;

CREATE INDEX [IX_ReportQueryParams_IsDeleted] ON [ReportQueryParams] ([IsDeleted]);

CREATE INDEX [IX_ReportQueryParams_TenantId] ON [ReportQueryParams] ([TenantId]);

CREATE UNIQUE INDEX [IX_ReportQueryParams_TenantId_ReportId_ParamCode] ON [ReportQueryParams] ([TenantId], [ReportId], [ParamCode]) WHERE [TenantId] IS NOT NULL AND [ReportId] IS NOT NULL AND [ParamCode] IS NOT NULL;

CREATE INDEX [IX_ReportQueryParams_TenantId_ReportId_Sort] ON [ReportQueryParams] ([TenantId], [ReportId], [Sort]);

CREATE INDEX [IX_ReportExecutionLogs_IsDeleted] ON [ReportExecutionLogs] ([IsDeleted]);

CREATE INDEX [IX_ReportExecutionLogs_TenantId] ON [ReportExecutionLogs] ([TenantId]);

CREATE INDEX [IX_ReportExecutionLogs_TenantId_ReportCode_CreatedAt] ON [ReportExecutionLogs] ([TenantId], [ReportCode], [CreatedAt]);

CREATE INDEX [IX_ReportExecutionLogs_TenantId_ReportId_CreatedAt] ON [ReportExecutionLogs] ([TenantId], [ReportId], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260526160000_AddReportCenter', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [SecurityPolicies] (
    [Id] uniqueidentifier NOT NULL,
    [PasswordMinLength] int NOT NULL DEFAULT 8,
    [RequireDigit] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RequireUppercase] bit NOT NULL,
    [RequireLowercase] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RequireSpecialChar] bit NOT NULL,
    [PasswordExpireDays] int NOT NULL,
    [LoginFailureLockThreshold] int NOT NULL DEFAULT 5,
    [LoginFailureLockMinutes] int NOT NULL DEFAULT 15,
    [EnableMfa] bit NOT NULL,
    [EnableSensitiveOperationVerify] bit NOT NULL,
    [EnableIpWhitelist] bit NOT NULL,
    [EnableIpBlacklist] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_SecurityPolicies] PRIMARY KEY ([Id])
);

CREATE TABLE [LoginFailureRecords] (
    [Id] uniqueidentifier NOT NULL,
    [UserName] nvarchar(100) NOT NULL,
    [IpAddress] nvarchar(64) NULL,
    [FailureCount] int NOT NULL,
    [LockedUntil] datetimeoffset NULL,
    [LastFailureAt] datetimeoffset NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_LoginFailureRecords] PRIMARY KEY ([Id])
);

CREATE TABLE [SensitiveOperationVerifications] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OperationCode] nvarchar(100) NOT NULL,
    [VerifyCode] nvarchar(32) NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [UsedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_SensitiveOperationVerifications] PRIMARY KEY ([Id])
);

CREATE TABLE [IpAccessRules] (
    [Id] uniqueidentifier NOT NULL,
    [RuleType] nvarchar(32) NOT NULL,
    [IpPattern] nvarchar(128) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsEnabled] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_IpAccessRules] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_SecurityPolicies_IsDeleted] ON [SecurityPolicies] ([IsDeleted]);

CREATE UNIQUE INDEX [IX_SecurityPolicies_TenantId] ON [SecurityPolicies] ([TenantId]) WHERE [TenantId] IS NOT NULL;

CREATE INDEX [IX_LoginFailureRecords_IsDeleted] ON [LoginFailureRecords] ([IsDeleted]);

CREATE INDEX [IX_LoginFailureRecords_TenantId] ON [LoginFailureRecords] ([TenantId]);

CREATE INDEX [IX_LoginFailureRecords_TenantId_LockedUntil] ON [LoginFailureRecords] ([TenantId], [LockedUntil]);

CREATE UNIQUE INDEX [IX_LoginFailureRecords_TenantId_UserName_IpAddress] ON [LoginFailureRecords] ([TenantId], [UserName], [IpAddress]) WHERE [IpAddress] IS NOT NULL;

CREATE INDEX [IX_SensitiveOperationVerifications_IsDeleted] ON [SensitiveOperationVerifications] ([IsDeleted]);

CREATE INDEX [IX_SensitiveOperationVerifications_TenantId] ON [SensitiveOperationVerifications] ([TenantId]);

CREATE INDEX [IX_SensitiveOperationVerifications_TenantId_UserId_OperationCode_ExpiresAt] ON [SensitiveOperationVerifications] ([TenantId], [UserId], [OperationCode], [ExpiresAt]);

CREATE INDEX [IX_SensitiveOperationVerifications_TenantId_VerifyCode] ON [SensitiveOperationVerifications] ([TenantId], [VerifyCode]);

CREATE INDEX [IX_IpAccessRules_IsDeleted] ON [IpAccessRules] ([IsDeleted]);

CREATE INDEX [IX_IpAccessRules_TenantId] ON [IpAccessRules] ([TenantId]);

CREATE UNIQUE INDEX [IX_IpAccessRules_TenantId_RuleType_IpPattern] ON [IpAccessRules] ([TenantId], [RuleType], [IpPattern]) WHERE [TenantId] IS NOT NULL AND [RuleType] IS NOT NULL AND [IpPattern] IS NOT NULL;

CREATE INDEX [IX_IpAccessRules_TenantId_RuleType_IsEnabled] ON [IpAccessRules] ([TenantId], [RuleType], [IsEnabled]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260526163000_AddSecurityPolicyCenter', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ApiClients] (
    [Id] uniqueidentifier NOT NULL,
    [ClientCode] nvarchar(100) NOT NULL,
    [ClientName] nvarchar(200) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsEnabled] bit NOT NULL,
    [AllowedScopes] nvarchar(1000) NULL,
    [AllowedIpList] nvarchar(1000) NULL,
    [RateLimitPerMinute] int NOT NULL DEFAULT 60,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_ApiClients] PRIMARY KEY ([Id])
);

CREATE TABLE [ApiClientSecrets] (
    [Id] uniqueidentifier NOT NULL,
    [ClientId] uniqueidentifier NOT NULL,
    [SecretHash] nvarchar(128) NOT NULL,
    [ExpiresAt] datetimeoffset NULL,
    [LastUsedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_ApiClientSecrets] PRIMARY KEY ([Id])
);

CREATE TABLE [WebhookSubscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [EventType] nvarchar(100) NOT NULL,
    [TargetUrl] nvarchar(1000) NOT NULL,
    [Secret] nvarchar(2000) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [RetryCount] int NOT NULL DEFAULT 3,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_WebhookSubscriptions] PRIMARY KEY ([Id])
);

CREATE TABLE [WebhookDeliveryLogs] (
    [Id] uniqueidentifier NOT NULL,
    [SubscriptionId] uniqueidentifier NOT NULL,
    [EventType] nvarchar(100) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [ResponseStatusCode] int NULL,
    [ResponseBody] nvarchar(4000) NULL,
    [RetryCount] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_WebhookDeliveryLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [ExternalApiCallLogs] (
    [Id] uniqueidentifier NOT NULL,
    [ClientId] uniqueidentifier NULL,
    [Path] nvarchar(500) NOT NULL,
    [Method] nvarchar(16) NOT NULL,
    [IpAddress] nvarchar(64) NULL,
    [StatusCode] int NOT NULL,
    [ElapsedMilliseconds] bigint NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_ExternalApiCallLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_ApiClients_IsDeleted] ON [ApiClients] ([IsDeleted]);

CREATE INDEX [IX_ApiClients_TenantId] ON [ApiClients] ([TenantId]);

CREATE UNIQUE INDEX [IX_ApiClients_TenantId_ClientCode] ON [ApiClients] ([TenantId], [ClientCode]) WHERE [TenantId] IS NOT NULL AND [ClientCode] IS NOT NULL;

CREATE INDEX [IX_ApiClients_TenantId_IsEnabled] ON [ApiClients] ([TenantId], [IsEnabled]);

CREATE INDEX [IX_ApiClientSecrets_IsDeleted] ON [ApiClientSecrets] ([IsDeleted]);

CREATE INDEX [IX_ApiClientSecrets_TenantId] ON [ApiClientSecrets] ([TenantId]);

CREATE INDEX [IX_ApiClientSecrets_TenantId_ClientId] ON [ApiClientSecrets] ([TenantId], [ClientId]);

CREATE UNIQUE INDEX [IX_ApiClientSecrets_TenantId_SecretHash] ON [ApiClientSecrets] ([TenantId], [SecretHash]) WHERE [TenantId] IS NOT NULL AND [SecretHash] IS NOT NULL;

CREATE INDEX [IX_WebhookSubscriptions_IsDeleted] ON [WebhookSubscriptions] ([IsDeleted]);

CREATE INDEX [IX_WebhookSubscriptions_TenantId] ON [WebhookSubscriptions] ([TenantId]);

CREATE INDEX [IX_WebhookSubscriptions_TenantId_EventType_IsEnabled] ON [WebhookSubscriptions] ([TenantId], [EventType], [IsEnabled]);

CREATE INDEX [IX_WebhookDeliveryLogs_IsDeleted] ON [WebhookDeliveryLogs] ([IsDeleted]);

CREATE INDEX [IX_WebhookDeliveryLogs_TenantId] ON [WebhookDeliveryLogs] ([TenantId]);

CREATE INDEX [IX_WebhookDeliveryLogs_TenantId_EventType_CreatedAt] ON [WebhookDeliveryLogs] ([TenantId], [EventType], [CreatedAt]);

CREATE INDEX [IX_WebhookDeliveryLogs_TenantId_Status_CreatedAt] ON [WebhookDeliveryLogs] ([TenantId], [Status], [CreatedAt]);

CREATE INDEX [IX_WebhookDeliveryLogs_TenantId_SubscriptionId_CreatedAt] ON [WebhookDeliveryLogs] ([TenantId], [SubscriptionId], [CreatedAt]);

CREATE INDEX [IX_ExternalApiCallLogs_IsDeleted] ON [ExternalApiCallLogs] ([IsDeleted]);

CREATE INDEX [IX_ExternalApiCallLogs_TenantId] ON [ExternalApiCallLogs] ([TenantId]);

CREATE INDEX [IX_ExternalApiCallLogs_TenantId_ClientId_CreatedAt] ON [ExternalApiCallLogs] ([TenantId], [ClientId], [CreatedAt]);

CREATE INDEX [IX_ExternalApiCallLogs_TenantId_Path_CreatedAt] ON [ExternalApiCallLogs] ([TenantId], [Path], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260526170000_AddOpenIntegrationCenter', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [sso_login_log] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderCode] nvarchar(100) NOT NULL,
    [ProviderName] nvarchar(200) NOT NULL,
    [ProviderType] nvarchar(32) NOT NULL,
    [ExternalUserId] nvarchar(256) NULL,
    [ExternalUserName] nvarchar(256) NULL,
    [LocalUserId] uniqueidentifier NULL,
    [LocalUserName] nvarchar(128) NULL,
    [LoginResult] nvarchar(32) NOT NULL,
    [FailureReason] nvarchar(512) NULL,
    [IpAddress] nvarchar(64) NULL,
    [UserAgent] nvarchar(512) NULL,
    [TraceId] nvarchar(128) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_sso_login_log] PRIMARY KEY ([Id])
);

CREATE TABLE [sso_provider] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderCode] nvarchar(100) NOT NULL,
    [ProviderName] nvarchar(200) NOT NULL,
    [ProviderType] nvarchar(32) NOT NULL,
    [Enabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [Authority] nvarchar(1000) NULL,
    [MetadataAddress] nvarchar(1000) NULL,
    [ClientId] nvarchar(256) NULL,
    [ClientSecretEncrypted] nvarchar(2000) NULL,
    [Scopes] nvarchar(1000) NOT NULL,
    [CallbackPath] nvarchar(256) NOT NULL,
    [ResponseType] nvarchar(64) NOT NULL,
    [UsePkce] bit NOT NULL DEFAULT CAST(1 AS bit),
    [GetClaimsFromUserInfoEndpoint] bit NOT NULL DEFAULT CAST(1 AS bit),
    [UserIdClaim] nvarchar(128) NOT NULL,
    [UserNameClaim] nvarchar(128) NOT NULL,
    [EmailClaim] nvarchar(128) NOT NULL,
    [PhoneClaim] nvarchar(128) NOT NULL,
    [DisplayNameClaim] nvarchar(128) NOT NULL,
    [RoleClaim] nvarchar(128) NOT NULL,
    [DepartmentClaim] nvarchar(128) NOT NULL,
    [AutoCreateUser] bit NOT NULL DEFAULT CAST(0 AS bit),
    [AutoBindUser] bit NOT NULL DEFAULT CAST(1 AS bit),
    [DefaultRoleIds] nvarchar(2000) NULL,
    [AllowLocalLoginFallback] bit NOT NULL DEFAULT CAST(1 AS bit),
    [LogoutRedirectUri] nvarchar(1000) NULL,
    [Remark] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_sso_provider] PRIMARY KEY ([Id])
);

CREATE TABLE [sso_department_mapping] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderId] uniqueidentifier NOT NULL,
    [ExternalDepartment] nvarchar(256) NOT NULL,
    [LocalDepartmentId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_sso_department_mapping] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_sso_department_mapping_Departments_LocalDepartmentId] FOREIGN KEY ([LocalDepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_sso_department_mapping_sso_provider_ProviderId] FOREIGN KEY ([ProviderId]) REFERENCES [sso_provider] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [sso_role_mapping] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderId] uniqueidentifier NOT NULL,
    [ExternalRole] nvarchar(256) NOT NULL,
    [LocalRoleId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_sso_role_mapping] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_sso_role_mapping_Roles_LocalRoleId] FOREIGN KEY ([LocalRoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_sso_role_mapping_sso_provider_ProviderId] FOREIGN KEY ([ProviderId]) REFERENCES [sso_provider] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [sso_user_binding] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderId] uniqueidentifier NOT NULL,
    [ProviderCode] nvarchar(100) NOT NULL,
    [ExternalUserId] nvarchar(256) NOT NULL,
    [ExternalUserName] nvarchar(256) NULL,
    [ExternalEmail] nvarchar(256) NULL,
    [ExternalPhone] nvarchar(64) NULL,
    [LocalUserId] uniqueidentifier NOT NULL,
    [LastLoginAt] datetimeoffset NULL,
    [ClaimsJson] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_sso_user_binding] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_sso_user_binding_Users_LocalUserId] FOREIGN KEY ([LocalUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_sso_user_binding_sso_provider_ProviderId] FOREIGN KEY ([ProviderId]) REFERENCES [sso_provider] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_sso_department_mapping_IsDeleted] ON [sso_department_mapping] ([IsDeleted]);

CREATE INDEX [IX_sso_department_mapping_LocalDepartmentId] ON [sso_department_mapping] ([LocalDepartmentId]);

CREATE INDEX [IX_sso_department_mapping_ProviderId] ON [sso_department_mapping] ([ProviderId]);

CREATE INDEX [IX_sso_department_mapping_TenantId] ON [sso_department_mapping] ([TenantId]);

CREATE INDEX [IX_sso_department_mapping_TenantId_ProviderId] ON [sso_department_mapping] ([TenantId], [ProviderId]);

CREATE UNIQUE INDEX [IX_sso_department_mapping_TenantId_ProviderId_ExternalDepartment_LocalDepartmentId] ON [sso_department_mapping] ([TenantId], [ProviderId], [ExternalDepartment], [LocalDepartmentId]);

CREATE INDEX [IX_sso_login_log_ExternalUserId] ON [sso_login_log] ([ExternalUserId]);

CREATE INDEX [IX_sso_login_log_IsDeleted] ON [sso_login_log] ([IsDeleted]);

CREATE INDEX [IX_sso_login_log_LocalUserId] ON [sso_login_log] ([LocalUserId]);

CREATE INDEX [IX_sso_login_log_TenantId] ON [sso_login_log] ([TenantId]);

CREATE INDEX [IX_sso_login_log_TenantId_CreatedAt] ON [sso_login_log] ([TenantId], [CreatedAt]);

CREATE INDEX [IX_sso_login_log_TenantId_LoginResult_CreatedAt] ON [sso_login_log] ([TenantId], [LoginResult], [CreatedAt]);

CREATE INDEX [IX_sso_login_log_TenantId_ProviderCode_CreatedAt] ON [sso_login_log] ([TenantId], [ProviderCode], [CreatedAt]);

CREATE INDEX [IX_sso_login_log_TraceId] ON [sso_login_log] ([TraceId]);

CREATE INDEX [IX_sso_provider_IsDeleted] ON [sso_provider] ([IsDeleted]);

CREATE INDEX [IX_sso_provider_TenantId] ON [sso_provider] ([TenantId]);

CREATE UNIQUE INDEX [IX_sso_provider_TenantId_ProviderCode] ON [sso_provider] ([TenantId], [ProviderCode]);

CREATE INDEX [IX_sso_provider_TenantId_ProviderType_Enabled] ON [sso_provider] ([TenantId], [ProviderType], [Enabled]);

CREATE INDEX [IX_sso_role_mapping_IsDeleted] ON [sso_role_mapping] ([IsDeleted]);

CREATE INDEX [IX_sso_role_mapping_LocalRoleId] ON [sso_role_mapping] ([LocalRoleId]);

CREATE INDEX [IX_sso_role_mapping_ProviderId] ON [sso_role_mapping] ([ProviderId]);

CREATE INDEX [IX_sso_role_mapping_TenantId] ON [sso_role_mapping] ([TenantId]);

CREATE INDEX [IX_sso_role_mapping_TenantId_ProviderId] ON [sso_role_mapping] ([TenantId], [ProviderId]);

CREATE UNIQUE INDEX [IX_sso_role_mapping_TenantId_ProviderId_ExternalRole_LocalRoleId] ON [sso_role_mapping] ([TenantId], [ProviderId], [ExternalRole], [LocalRoleId]);

CREATE INDEX [IX_sso_user_binding_IsDeleted] ON [sso_user_binding] ([IsDeleted]);

CREATE INDEX [IX_sso_user_binding_LocalUserId] ON [sso_user_binding] ([LocalUserId]);

CREATE INDEX [IX_sso_user_binding_ProviderId] ON [sso_user_binding] ([ProviderId]);

CREATE INDEX [IX_sso_user_binding_TenantId] ON [sso_user_binding] ([TenantId]);

CREATE INDEX [IX_sso_user_binding_TenantId_ExternalEmail] ON [sso_user_binding] ([TenantId], [ExternalEmail]);

CREATE INDEX [IX_sso_user_binding_TenantId_ExternalPhone] ON [sso_user_binding] ([TenantId], [ExternalPhone]);

CREATE INDEX [IX_sso_user_binding_TenantId_ProviderCode] ON [sso_user_binding] ([TenantId], [ProviderCode]);

CREATE UNIQUE INDEX [IX_sso_user_binding_TenantId_ProviderId_ExternalUserId] ON [sso_user_binding] ([TenantId], [ProviderId], [ExternalUserId]);

CREATE UNIQUE INDEX [IX_sso_user_binding_TenantId_ProviderId_LocalUserId] ON [sso_user_binding] ([TenantId], [ProviderId], [LocalUserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260608094216_AddSsoBaseEntities', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_wf_task_TenantId_ApproverUserId_Status_CreatedAt] ON [wf_task];

DROP INDEX [IX_UserNotifications_UserId_NotificationId] ON [UserNotifications];

DROP INDEX [IX_sso_login_log_ExternalUserId] ON [sso_login_log];

DROP INDEX [IX_sso_login_log_LocalUserId] ON [sso_login_log];

DROP INDEX [IX_sso_login_log_TraceId] ON [sso_login_log];

DROP INDEX [IX_OperationLogs_TraceId] ON [OperationLogs];

DROP INDEX [IX_OperationLogs_UserId] ON [OperationLogs];

DROP INDEX [IX_LoginLogs_TraceId] ON [LoginLogs];

DROP INDEX [IX_LoginLogs_UserId] ON [LoginLogs];

DROP INDEX [IX_FileResources_TenantId_BusinessType_BusinessId] ON [FileResources];

CREATE INDEX [IX_wf_task_TenantId_ApproverUserId_Status_AssignedAt] ON [wf_task] ([TenantId], [ApproverUserId], [Status], [AssignedAt]);

CREATE INDEX [IX_wf_task_TenantId_InstanceId_ApproverUserId] ON [wf_task] ([TenantId], [InstanceId], [ApproverUserId]);

CREATE INDEX [IX_Users_TenantId_Email] ON [Users] ([TenantId], [Email]);

CREATE INDEX [IX_Users_TenantId_PhoneNumber] ON [Users] ([TenantId], [PhoneNumber]);

CREATE UNIQUE INDEX [IX_UserNotifications_TenantId_UserId_NotificationId] ON [UserNotifications] ([TenantId], [UserId], [NotificationId]);

CREATE INDEX [IX_sso_login_log_TenantId_LocalUserId_CreatedAt] ON [sso_login_log] ([TenantId], [LocalUserId], [CreatedAt]);

CREATE INDEX [IX_sso_login_log_TenantId_ProviderCode_ExternalUserId_CreatedAt] ON [sso_login_log] ([TenantId], [ProviderCode], [ExternalUserId], [CreatedAt]);

CREATE INDEX [IX_sso_login_log_TenantId_TraceId] ON [sso_login_log] ([TenantId], [TraceId]);

CREATE INDEX [IX_Roles_TenantId_IsEnabled_Sort] ON [Roles] ([TenantId], [IsEnabled], [Sort]);

CREATE INDEX [IX_ReportExecutionLogs_TenantId_CreatedAt] ON [ReportExecutionLogs] ([TenantId], [CreatedAt]);

CREATE INDEX [IX_Permissions_TenantId_Group] ON [Permissions] ([TenantId], [Group]);

CREATE INDEX [IX_OutboxMessages_Status_NextRetryAt_CreatedAt] ON [OutboxMessages] ([Status], [NextRetryAt], [CreatedAt]);

CREATE INDEX [IX_OutboxMessages_TenantId_Status_CreatedAt] ON [OutboxMessages] ([TenantId], [Status], [CreatedAt]);

CREATE INDEX [IX_OperationLogs_TenantId_TraceId] ON [OperationLogs] ([TenantId], [TraceId]);

CREATE INDEX [IX_OperationLogs_TenantId_UserId_CreatedAt] ON [OperationLogs] ([TenantId], [UserId], [CreatedAt]);

CREATE INDEX [IX_Menus_TenantId_ParentId_Sort] ON [Menus] ([TenantId], [ParentId], [Sort]);

CREATE INDEX [IX_LoginLogs_TenantId_TraceId] ON [LoginLogs] ([TenantId], [TraceId]);

CREATE INDEX [IX_LoginLogs_TenantId_UserId_CreatedAt] ON [LoginLogs] ([TenantId], [UserId], [CreatedAt]);

CREATE INDEX [IX_InboxMessages_TenantId_Status_CreatedAt] ON [InboxMessages] ([TenantId], [Status], [CreatedAt]);

CREATE INDEX [IX_FileResources_TenantId_BusinessType_BusinessId_CreatedAt] ON [FileResources] ([TenantId], [BusinessType], [BusinessId], [CreatedAt]);

CREATE INDEX [IX_ExternalApiCallLogs_TenantId_CreatedAt] ON [ExternalApiCallLogs] ([TenantId], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260611023144_AddDatabasePerformanceIndexes', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [demo_business_order] (
    [Id] uniqueidentifier NOT NULL,
    [OrderNo] nvarchar(100) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [CustomerName] nvarchar(200) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [DepartmentId] uniqueidentifier NULL,
    [OwnerUserId] uniqueidentifier NOT NULL,
    [OwnerUserName] nvarchar(100) NOT NULL,
    [ApprovalStatus] int NOT NULL DEFAULT 0,
    [WorkflowInstanceId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    [SubmittedBy] uniqueidentifier NULL,
    [ApprovedAt] datetimeoffset NULL,
    [RejectedAt] datetimeoffset NULL,
    [WithdrawnAt] datetimeoffset NULL,
    [ChangeHistoryJson] nvarchar(max) NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_demo_business_order] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_demo_business_order_IsDeleted] ON [demo_business_order] ([IsDeleted]);

CREATE INDEX [IX_demo_business_order_TenantId] ON [demo_business_order] ([TenantId]);

CREATE INDEX [IX_demo_business_order_TenantId_ApprovalStatus_CreatedAt] ON [demo_business_order] ([TenantId], [ApprovalStatus], [CreatedAt]);

CREATE INDEX [IX_demo_business_order_TenantId_DepartmentId_CreatedAt] ON [demo_business_order] ([TenantId], [DepartmentId], [CreatedAt]);

CREATE UNIQUE INDEX [IX_demo_business_order_TenantId_OrderNo_IsDeleted] ON [demo_business_order] ([TenantId], [OrderNo], [IsDeleted]) WHERE [TenantId] IS NOT NULL AND [OrderNo] IS NOT NULL AND [IsDeleted] IS NOT NULL;

CREATE INDEX [IX_demo_business_order_TenantId_OwnerUserId_CreatedAt] ON [demo_business_order] ([TenantId], [OwnerUserId], [CreatedAt]);

CREATE INDEX [IX_demo_business_order_TenantId_WorkflowInstanceId] ON [demo_business_order] ([TenantId], [WorkflowInstanceId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260611090000_AddDemoBusinessOrder', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Tenants] ADD [InitializationAttempts] int NOT NULL DEFAULT 0;

ALTER TABLE [Tenants] ADD [InitializationError] nvarchar(2000) NULL;

ALTER TABLE [Tenants] ADD [InitializationJobId] nvarchar(128) NULL;

ALTER TABLE [Tenants] ADD [InitializationProgress] int NOT NULL DEFAULT 0;

ALTER TABLE [Tenants] ADD [InitializationStartedAt] datetimeoffset NULL;

ALTER TABLE [Tenants] ADD [InitializationStep] nvarchar(64) NULL;

ALTER TABLE [Tenants] ADD [InitializedAt] datetimeoffset NULL;

ALTER TABLE [Tenants] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [Tenants] ADD [Status] int NOT NULL DEFAULT 0;

ALTER TABLE [Tenants] ADD [StatusChangedAt] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';

UPDATE Tenants
SET Status = CASE WHEN IsEnabled = 1 THEN 1 ELSE 2 END,
    InitializationStep = 'Completed',
    InitializationProgress = 100,
    InitializedAt = CreatedAt,
    StatusChangedAt = COALESCE(UpdatedAt, CreatedAt)

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tenants]') AND [c].[name] = N'IsEnabled');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Tenants] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [Tenants] DROP COLUMN [IsEnabled];

CREATE INDEX [IX_Tenants_Status] ON [Tenants] ([Status]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260805005948_EA008TenantLifecycle', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Users] ADD [SecurityStamp] uniqueidentifier NOT NULL DEFAULT (NEWID());

UPDATE sessions
SET sessions.IsRevoked = 1,
    sessions.RevokedAt = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
    sessions.RevokedReason = 'Revoked during EA-010 rollout because the user is inactive.'
FROM UserSessions AS sessions
INNER JOIN Users AS users
    ON users.TenantId = sessions.TenantId
    AND users.Id = sessions.UserId
WHERE sessions.IsDeleted = 0
    AND sessions.IsRevoked = 0
    AND (users.IsDeleted = 1 OR users.IsEnabled = 0);

CREATE INDEX [IX_UserRoles_TenantId_RoleId_UserId] ON [UserRoles] ([TenantId], [RoleId], [UserId]);

CREATE INDEX [IX_RolePermissions_TenantId_PermissionId_RoleId] ON [RolePermissions] ([TenantId], [PermissionId], [RoleId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260806012057_EA010AuthorizationInvalidation', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_SensitiveOperationVerifications_TenantId_UserId_OperationCode_ExpiresAt] ON [SensitiveOperationVerifications];

DROP INDEX [IX_SensitiveOperationVerifications_TenantId_VerifyCode] ON [SensitiveOperationVerifications];

DELETE FROM [SensitiveOperationVerifications];

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SensitiveOperationVerifications]') AND [c].[name] = N'VerifyCode');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [SensitiveOperationVerifications] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [SensitiveOperationVerifications] DROP COLUMN [VerifyCode];

ALTER TABLE [SensitiveOperationVerifications] ADD [VerificationMethod] nvarchar(32) NOT NULL DEFAULT N'Password';

ALTER TABLE [SensitiveOperationVerifications] ADD [FailedAttemptCount] int NOT NULL DEFAULT 0;

ALTER TABLE [SensitiveOperationVerifications] ADD [LockedAt] datetimeoffset NULL;

ALTER TABLE [SensitiveOperationVerifications] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [SensitiveOperationVerifications] ADD [SessionId] nvarchar(128) NOT NULL DEFAULT N'';

ALTER TABLE [SensitiveOperationVerifications] ADD [TicketExpiresAt] datetimeoffset NULL;

ALTER TABLE [SensitiveOperationVerifications] ADD [TicketHash] nvarchar(64) NULL;

ALTER TABLE [SensitiveOperationVerifications] ADD [VerifiedAt] datetimeoffset NULL;

CREATE UNIQUE INDEX [IX_SensitiveOperationVerifications_TenantId_TicketHash] ON [SensitiveOperationVerifications] ([TenantId], [TicketHash]) WHERE [TicketHash] IS NOT NULL;

CREATE INDEX [IX_SensitiveOperationVerifications_TenantId_UserId_SessionId_OperationCode_ExpiresAt] ON [SensitiveOperationVerifications] ([TenantId], [UserId], [SessionId], [OperationCode], [ExpiresAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260807084524_EA012StepUpAuthentication', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [ReportExecutionLogs] ADD [FailureReason] nvarchar(500) NULL;

ALTER TABLE [ReportExecutionLogs] ADD [IsSuccess] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [ReportDefinitions] ADD [DatasetKey] nvarchar(100) NULL;

UPDATE ReportExecutionLogs SET IsSuccess = 1 WHERE IsSuccess = 0;

UPDATE ReportDefinitions SET DatasetKey = CASE ReportCode WHEN 'SystemUserList' THEN 'system-users' WHEN 'SystemLoginLogs' THEN 'system-login-logs' WHEN 'SystemOperationLogs' THEN 'system-operation-logs' ELSE DatasetKey END WHERE ReportCode IN ('SystemUserList', 'SystemLoginLogs', 'SystemOperationLogs');

UPDATE ReportDefinitions SET SqlText = NULL WHERE DataSourceType = 'Sql';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260808064554_EA016ReportExecutionIsolation', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_wf_instance_TenantId_BusinessType_BusinessId] ON [wf_instance];

ALTER TABLE [wf_task] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [wf_instance] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [demo_business_order] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [demo_approval_order] ADD [RowVersion] rowversion NOT NULL;

IF EXISTS (SELECT 1 FROM [wf_instance] WHERE [Status] = 0 AND [IsDeleted] = 0 GROUP BY [TenantId], [BusinessType], [BusinessId] HAVING COUNT(*) > 1) THROW 51000, 'EA-017 migration blocked: duplicate running workflow instances must be resolved before creating the unique index.', 1;

CREATE UNIQUE INDEX [IX_wf_instance_TenantId_BusinessType_BusinessId] ON [wf_instance] ([TenantId], [BusinessType], [BusinessId]) WHERE [Status] = 0 AND [IsDeleted] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260808073525_EA017WorkflowConcurrencyControl', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [FileResources] ADD [DeletedAt] datetimeoffset NULL;

ALTER TABLE [FileResources] ADD [FileStatus] int NOT NULL DEFAULT 0;

ALTER TABLE [FileResources] ADD [LastError] nvarchar(2000) NULL;

ALTER TABLE [FileResources] ADD [NextRetryAt] datetimeoffset NULL;

ALTER TABLE [FileResources] ADD [RetryCount] int NOT NULL DEFAULT 0;

ALTER TABLE [FileResources] ADD [ScanMessage] nvarchar(2000) NULL;

ALTER TABLE [FileResources] ADD [ScanStatus] int NOT NULL DEFAULT 0;

ALTER TABLE [FileResources] ADD [Sha256] nvarchar(64) NOT NULL DEFAULT N'';

UPDATE [FileResources] SET [FileStatus] = 1, [ScanStatus] = 1 WHERE [FileStatus] = 0 AND [ScanStatus] = 0;

CREATE INDEX [IX_FileResources_FileStatus_NextRetryAt_CreatedAt] ON [FileResources] ([FileStatus], [NextRetryAt], [CreatedAt]);

CREATE INDEX [IX_FileResources_TenantId_FileStatus_ScanStatus_CreatedAt] ON [FileResources] ([TenantId], [FileStatus], [ScanStatus], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260808094958_EA019FileSecurityAclCompensation', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
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

DROP INDEX [IX_wf_node_TenantId_DefinitionId_NodeKey] ON [wf_node];

DROP INDEX [IX_wf_definition_TenantId_Code_Version] ON [wf_definition];

DROP INDEX [IX_wf_business_binding_TenantId_BusinessType_IsDeleted] ON [wf_business_binding];

DROP INDEX [IX_Users_TenantId_NormalizedUserName] ON [Users];

DROP INDEX [IX_UserRoles_TenantId_UserId_RoleId] ON [UserRoles];

DROP INDEX [IX_UserDataScopes_TenantId_UserId] ON [UserDataScopes];

DROP INDEX [IX_SystemConfigs_TenantId_ConfigKey] ON [SystemConfigs];

DROP INDEX [IX_StateMachineDefinitions_TenantId_BusinessType] ON [StateMachineDefinitions];

DROP INDEX [IX_StateDefinitions_TenantId_MachineId_StateCode] ON [StateDefinitions];

DROP INDEX [IX_sso_user_binding_TenantId_ProviderId_ExternalUserId] ON [sso_user_binding];

DROP INDEX [IX_sso_user_binding_TenantId_ProviderId_LocalUserId] ON [sso_user_binding];

DROP INDEX [IX_sso_role_mapping_TenantId_ProviderId_ExternalRole_LocalRoleId] ON [sso_role_mapping];

DROP INDEX [IX_sso_provider_TenantId_ProviderCode] ON [sso_provider];

DROP INDEX [IX_sso_department_mapping_TenantId_ProviderId_ExternalDepartment_LocalDepartmentId] ON [sso_department_mapping];

DROP INDEX [IX_ScheduledTasks_TenantId_Code] ON [ScheduledTasks];

DROP INDEX [IX_Roles_TenantId_Code] ON [Roles];

DROP INDEX [IX_RolePermissions_TenantId_RoleId_PermissionId] ON [RolePermissions];

DROP INDEX [IX_RoleMenus_TenantId_RoleId_MenuId] ON [RoleMenus];

DROP INDEX [IX_RoleDataScopes_TenantId_RoleId] ON [RoleDataScopes];

DROP INDEX [IX_ReportQueryParams_TenantId_ReportId_ParamCode] ON [ReportQueryParams];

DROP INDEX [IX_ReportDefinitions_TenantId_ReportCode] ON [ReportDefinitions];

DROP INDEX [IX_PrintTemplates_TenantId_TemplateCode] ON [PrintTemplates];

DROP INDEX [IX_Permissions_TenantId_Code] ON [Permissions];

DROP INDEX [IX_NumberRules_TenantId_RuleCode] ON [NumberRules];

DROP INDEX [IX_NotificationTemplates_TenantId_Code] ON [NotificationTemplates];

DROP INDEX [IX_LoginFailureRecords_TenantId_UserName_IpAddress] ON [LoginFailureRecords];

DROP INDEX [IX_IpAccessRules_TenantId_RuleType_IpPattern] ON [IpAccessRules];

DROP INDEX [IX_DictionaryTypes_TenantId_Code] ON [DictionaryTypes];

DROP INDEX [IX_DictionaryItems_TenantId_TypeCode_Value] ON [DictionaryItems];

DROP INDEX [IX_Departments_TenantId_Code] ON [Departments];

DROP INDEX [IX_demo_business_order_TenantId_OrderNo_IsDeleted] ON [demo_business_order];

DROP INDEX [IX_demo_approval_order_TenantId_OrderNo_IsDeleted] ON [demo_approval_order];

DROP INDEX [IX_ApiClients_TenantId_ClientCode] ON [ApiClients];

ALTER TABLE [wf_record] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [wf_node] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [wf_edge] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [wf_definition] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [wf_condition] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [wf_cc] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [wf_business_binding] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [WebhookSubscriptions] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [WebhookDeliveryLogs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [UserSessions] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [Users] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [UserRoles] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [UserNotifications] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [UserDataScopes] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [SystemConfigs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [StateTransitions] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [StateTransitionLogs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [StateMachineDefinitions] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [StateDefinitions] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [sso_user_binding] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [sso_role_mapping] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [sso_provider] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [sso_login_log] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [sso_department_mapping] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [SecurityPolicies] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [ScheduledTasks] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [ScheduledTaskExecutionLogs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [Roles] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [RolePermissions] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [RoleMenus] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [RoleDataScopes] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [ReportQueryParams] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [ReportExecutionLogs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [ReportDefinitions] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [PrintTemplates] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [PrintRecords] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [Permissions] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [OutboxMessages] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [OperationLogs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [NumberSequences] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [NumberRuleSegments] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [NumberRules] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [NotificationTemplates] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [Notifications] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [Menus] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [LoginLogs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [LoginFailureRecords] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [JobExecutionLogs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [IpAccessRules] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [InboxMessages] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [FileResources] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [ExternalApiCallLogs] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [DictionaryTypes] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [DictionaryItems] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [Departments] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [ApiClientSecrets] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [ApiClients] ADD [RowVersion] rowversion NOT NULL;

CREATE UNIQUE INDEX [IX_wf_node_TenantId_DefinitionId_NodeKey] ON [wf_node] ([TenantId], [DefinitionId], [NodeKey]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_wf_definition_TenantId_Code_Version] ON [wf_definition] ([TenantId], [Code], [Version]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_wf_business_binding_TenantId_BusinessType] ON [wf_business_binding] ([TenantId], [BusinessType]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_Users_TenantId_NormalizedUserName] ON [Users] ([TenantId], [NormalizedUserName]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_UserRoles_TenantId_UserId_RoleId] ON [UserRoles] ([TenantId], [UserId], [RoleId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_UserDataScopes_TenantId_UserId] ON [UserDataScopes] ([TenantId], [UserId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_SystemConfigs_TenantId_ConfigKey] ON [SystemConfigs] ([TenantId], [ConfigKey]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_StateMachineDefinitions_TenantId_BusinessType] ON [StateMachineDefinitions] ([TenantId], [BusinessType]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_StateDefinitions_TenantId_MachineId_StateCode] ON [StateDefinitions] ([TenantId], [MachineId], [StateCode]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_sso_user_binding_TenantId_ProviderId_ExternalUserId] ON [sso_user_binding] ([TenantId], [ProviderId], [ExternalUserId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_sso_user_binding_TenantId_ProviderId_LocalUserId] ON [sso_user_binding] ([TenantId], [ProviderId], [LocalUserId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_sso_role_mapping_TenantId_ProviderId_ExternalRole_LocalRoleId] ON [sso_role_mapping] ([TenantId], [ProviderId], [ExternalRole], [LocalRoleId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_sso_provider_TenantId_ProviderCode] ON [sso_provider] ([TenantId], [ProviderCode]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_sso_department_mapping_TenantId_ProviderId_ExternalDepartment_LocalDepartmentId] ON [sso_department_mapping] ([TenantId], [ProviderId], [ExternalDepartment], [LocalDepartmentId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_ScheduledTasks_TenantId_Code] ON [ScheduledTasks] ([TenantId], [Code]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_Roles_TenantId_Code] ON [Roles] ([TenantId], [Code]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_RolePermissions_TenantId_RoleId_PermissionId] ON [RolePermissions] ([TenantId], [RoleId], [PermissionId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_RoleMenus_TenantId_RoleId_MenuId] ON [RoleMenus] ([TenantId], [RoleId], [MenuId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_RoleDataScopes_TenantId_RoleId] ON [RoleDataScopes] ([TenantId], [RoleId]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_ReportQueryParams_TenantId_ReportId_ParamCode] ON [ReportQueryParams] ([TenantId], [ReportId], [ParamCode]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_ReportDefinitions_TenantId_ReportCode] ON [ReportDefinitions] ([TenantId], [ReportCode]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_PrintTemplates_TenantId_TemplateCode] ON [PrintTemplates] ([TenantId], [TemplateCode]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_Permissions_TenantId_Code] ON [Permissions] ([TenantId], [Code]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_NumberRules_TenantId_RuleCode] ON [NumberRules] ([TenantId], [RuleCode]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_NotificationTemplates_TenantId_Code] ON [NotificationTemplates] ([TenantId], [Code]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_LoginFailureRecords_TenantId_UserName_IpAddress] ON [LoginFailureRecords] ([TenantId], [UserName], [IpAddress]) WHERE [IsDeleted] = 0 AND [IpAddress] IS NOT NULL;

CREATE UNIQUE INDEX [IX_IpAccessRules_TenantId_RuleType_IpPattern] ON [IpAccessRules] ([TenantId], [RuleType], [IpPattern]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_DictionaryTypes_TenantId_Code] ON [DictionaryTypes] ([TenantId], [Code]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_DictionaryItems_TenantId_TypeCode_Value] ON [DictionaryItems] ([TenantId], [TypeCode], [Value]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_Departments_TenantId_Code] ON [Departments] ([TenantId], [Code]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_demo_business_order_TenantId_OrderNo] ON [demo_business_order] ([TenantId], [OrderNo]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_demo_approval_order_TenantId_OrderNo] ON [demo_approval_order] ([TenantId], [OrderNo]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_ApiClients_TenantId_ClientCode] ON [ApiClients] ([TenantId], [ClientCode]) WHERE [IsDeleted] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260810100449_EA022SoftDeleteConcurrency', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [InboxMessages] ADD [ErrorMessage] nvarchar(2000) NULL;

CREATE TABLE [DeadLetterMessages] (
    [Id] uniqueidentifier NOT NULL,
    [MessageId] nvarchar(64) NOT NULL,
    [Consumer] nvarchar(128) NOT NULL,
    [SourceQueue] nvarchar(128) NOT NULL,
    [Exchange] nvarchar(128) NOT NULL,
    [RoutingKey] nvarchar(256) NOT NULL,
    [MessageType] nvarchar(256) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Headers] nvarchar(max) NULL,
    [RetryCount] int NOT NULL,
    [FailureReason] nvarchar(2000) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [ReplayCount] int NOT NULL,
    [LastReplayedAt] datetimeoffset NULL,
    [DispositionRemark] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_DeadLetterMessages] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_DeadLetterMessages_IsDeleted] ON [DeadLetterMessages] ([IsDeleted]);

CREATE INDEX [IX_DeadLetterMessages_TenantId] ON [DeadLetterMessages] ([TenantId]);

CREATE UNIQUE INDEX [IX_DeadLetterMessages_TenantId_MessageId_Consumer] ON [DeadLetterMessages] ([TenantId], [MessageId], [Consumer]);

CREATE INDEX [IX_DeadLetterMessages_TenantId_SourceQueue_CreatedAt] ON [DeadLetterMessages] ([TenantId], [SourceQueue], [CreatedAt]);

CREATE INDEX [IX_DeadLetterMessages_TenantId_Status_CreatedAt] ON [DeadLetterMessages] ([TenantId], [Status], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260810132207_EA024RabbitMqDeadLetterGovernance', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ai_conversation] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [AgentCode] nvarchar(100) NOT NULL,
    [AgentVersion] nvarchar(64) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [LastMessageAt] datetimeoffset NOT NULL,
    [LastRunAt] datetimeoffset NULL,
    [RetentionUntil] datetimeoffset NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_conversation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_conversation_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_provider_config] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderCode] nvarchar(100) NOT NULL,
    [ProviderName] nvarchar(200) NOT NULL,
    [ProviderType] nvarchar(32) NOT NULL,
    [BaseUrl] nvarchar(1000) NOT NULL,
    [ChatCompletionsPath] nvarchar(256) NOT NULL,
    [ApiKeyEncrypted] nvarchar(2000) NOT NULL,
    [ModelName] nvarchar(200) NOT NULL,
    [IsDefault] bit NOT NULL,
    [IsEnabled] bit NOT NULL,
    [TimeoutSeconds] int NOT NULL DEFAULT 30,
    [Temperature] decimal(5,4) NULL,
    [MaxTokens] int NULL,
    [AllowInsecureHttp] bit NOT NULL,
    [AllowPrivateNetwork] bit NOT NULL,
    [AllowedHostsJson] nvarchar(4000) NOT NULL,
    [DataResidency] nvarchar(100) NULL,
    [Remark] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_provider_config] PRIMARY KEY ([Id])
);

CREATE TABLE [ai_message] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [Role] nvarchar(32) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [ContentClassification] nvarchar(32) NOT NULL,
    [ContentDigest] nvarchar(128) NOT NULL,
    [TokenCount] int NULL,
    [Sequence] int NOT NULL,
    [ModelGenerated] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_message] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_message_ai_conversation_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [ai_conversation] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_run] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [RequestMessageId] uniqueidentifier NOT NULL,
    [ResponseMessageId] uniqueidentifier NULL,
    [ProviderConfigId] uniqueidentifier NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [AgentCode] nvarchar(100) NOT NULL,
    [AgentVersion] nvarchar(64) NOT NULL,
    [PromptVersion] nvarchar(64) NOT NULL,
    [ModelName] nvarchar(200) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [TraceId] nvarchar(128) NOT NULL,
    [StartedAt] datetimeoffset NULL,
    [CompletedAt] datetimeoffset NULL,
    [DurationMilliseconds] bigint NULL,
    [InputTokens] int NULL,
    [OutputTokens] int NULL,
    [EstimatedCost] decimal(18,6) NULL,
    [ErrorCode] nvarchar(100) NULL,
    [ErrorSummary] nvarchar(1000) NULL,
    [CancellationRequestedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_run] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_run_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_run_ai_conversation_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [ai_conversation] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_run_ai_message_RequestMessageId] FOREIGN KEY ([RequestMessageId]) REFERENCES [ai_message] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_run_ai_message_ResponseMessageId] FOREIGN KEY ([ResponseMessageId]) REFERENCES [ai_message] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_run_ai_provider_config_ProviderConfigId] FOREIGN KEY ([ProviderConfigId]) REFERENCES [ai_provider_config] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_tool_invocation] (
    [Id] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [InvocationId] nvarchar(128) NOT NULL,
    [ToolCode] nvarchar(200) NOT NULL,
    [ToolVersion] nvarchar(64) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [InputDigest] nvarchar(128) NOT NULL,
    [OutputDigest] nvarchar(128) NULL,
    [SourceSystem] nvarchar(100) NOT NULL,
    [DatasetCode] nvarchar(100) NULL,
    [DatasetVersion] nvarchar(64) NULL,
    [RowCount] int NULL,
    [IsTruncated] bit NOT NULL,
    [StartedAt] datetimeoffset NULL,
    [CompletedAt] datetimeoffset NULL,
    [DurationMilliseconds] bigint NULL,
    [RetryCount] int NOT NULL,
    [ErrorCode] nvarchar(100) NULL,
    [CitationJson] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_tool_invocation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_tool_invocation_ai_run_RunId] FOREIGN KEY ([RunId]) REFERENCES [ai_run] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_usage_log] (
    [Id] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [ProviderConfigId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    [ModelName] nvarchar(200) NOT NULL,
    [ProviderRequestId] nvarchar(200) NULL,
    [Status] nvarchar(32) NOT NULL,
    [InputTokens] int NULL,
    [OutputTokens] int NULL,
    [TotalTokens] int NULL,
    [EstimatedCost] decimal(18,6) NULL,
    [StartedAt] datetimeoffset NULL,
    [CompletedAt] datetimeoffset NULL,
    [DurationMilliseconds] bigint NULL,
    [RetryCount] int NOT NULL,
    [FinishReason] nvarchar(100) NULL,
    [ErrorCode] nvarchar(100) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_usage_log] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_usage_log_ai_provider_config_ProviderConfigId] FOREIGN KEY ([ProviderConfigId]) REFERENCES [ai_provider_config] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_usage_log_ai_run_RunId] FOREIGN KEY ([RunId]) REFERENCES [ai_run] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ai_conversation_IsDeleted] ON [ai_conversation] ([IsDeleted]);

CREATE INDEX [IX_ai_conversation_TenantId] ON [ai_conversation] ([TenantId]);

CREATE INDEX [IX_ai_conversation_TenantId_RetentionUntil] ON [ai_conversation] ([TenantId], [RetentionUntil]);

CREATE INDEX [IX_ai_conversation_TenantId_Status_LastMessageAt] ON [ai_conversation] ([TenantId], [Status], [LastMessageAt]);

CREATE INDEX [IX_ai_conversation_TenantId_UserId_LastMessageAt] ON [ai_conversation] ([TenantId], [UserId], [LastMessageAt]);

CREATE INDEX [IX_ai_conversation_UserId] ON [ai_conversation] ([UserId]);

CREATE INDEX [IX_ai_message_ConversationId] ON [ai_message] ([ConversationId]);

CREATE INDEX [IX_ai_message_IsDeleted] ON [ai_message] ([IsDeleted]);

CREATE INDEX [IX_ai_message_TenantId] ON [ai_message] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_message_TenantId_ConversationId_Sequence] ON [ai_message] ([TenantId], [ConversationId], [Sequence]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_provider_config_IsDeleted] ON [ai_provider_config] ([IsDeleted]);

CREATE INDEX [IX_ai_provider_config_TenantId] ON [ai_provider_config] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_provider_config_TenantId_IsDefault] ON [ai_provider_config] ([TenantId], [IsDefault]) WHERE [IsDefault] = 1 AND [IsDeleted] = 0;

CREATE INDEX [IX_ai_provider_config_TenantId_IsEnabled_IsDefault] ON [ai_provider_config] ([TenantId], [IsEnabled], [IsDefault]);

CREATE UNIQUE INDEX [IX_ai_provider_config_TenantId_ProviderCode] ON [ai_provider_config] ([TenantId], [ProviderCode]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_run_ActorUserId] ON [ai_run] ([ActorUserId]);

CREATE INDEX [IX_ai_run_ConversationId] ON [ai_run] ([ConversationId]);

CREATE INDEX [IX_ai_run_IsDeleted] ON [ai_run] ([IsDeleted]);

CREATE INDEX [IX_ai_run_ProviderConfigId] ON [ai_run] ([ProviderConfigId]);

CREATE INDEX [IX_ai_run_RequestMessageId] ON [ai_run] ([RequestMessageId]);

CREATE INDEX [IX_ai_run_ResponseMessageId] ON [ai_run] ([ResponseMessageId]);

CREATE INDEX [IX_ai_run_TenantId] ON [ai_run] ([TenantId]);

CREATE INDEX [IX_ai_run_TenantId_ActorUserId_CreatedAt] ON [ai_run] ([TenantId], [ActorUserId], [CreatedAt]);

CREATE INDEX [IX_ai_run_TenantId_ConversationId_CreatedAt] ON [ai_run] ([TenantId], [ConversationId], [CreatedAt]);

CREATE INDEX [IX_ai_run_TenantId_Status_CreatedAt] ON [ai_run] ([TenantId], [Status], [CreatedAt]);

CREATE INDEX [IX_ai_run_TenantId_TraceId] ON [ai_run] ([TenantId], [TraceId]);

CREATE INDEX [IX_ai_tool_invocation_IsDeleted] ON [ai_tool_invocation] ([IsDeleted]);

CREATE INDEX [IX_ai_tool_invocation_RunId] ON [ai_tool_invocation] ([RunId]);

CREATE INDEX [IX_ai_tool_invocation_TenantId] ON [ai_tool_invocation] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_tool_invocation_TenantId_InvocationId] ON [ai_tool_invocation] ([TenantId], [InvocationId]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_tool_invocation_TenantId_RunId_CreatedAt] ON [ai_tool_invocation] ([TenantId], [RunId], [CreatedAt]);

CREATE INDEX [IX_ai_tool_invocation_TenantId_Status_CreatedAt] ON [ai_tool_invocation] ([TenantId], [Status], [CreatedAt]);

CREATE INDEX [IX_ai_tool_invocation_TenantId_ToolCode_CreatedAt] ON [ai_tool_invocation] ([TenantId], [ToolCode], [CreatedAt]);

CREATE INDEX [IX_ai_usage_log_IsDeleted] ON [ai_usage_log] ([IsDeleted]);

CREATE INDEX [IX_ai_usage_log_ProviderConfigId] ON [ai_usage_log] ([ProviderConfigId]);

CREATE INDEX [IX_ai_usage_log_RunId] ON [ai_usage_log] ([RunId]);

CREATE INDEX [IX_ai_usage_log_TenantId] ON [ai_usage_log] ([TenantId]);

CREATE INDEX [IX_ai_usage_log_TenantId_ProviderConfigId_CreatedAt] ON [ai_usage_log] ([TenantId], [ProviderConfigId], [CreatedAt]);

CREATE UNIQUE INDEX [IX_ai_usage_log_TenantId_RunId_Sequence] ON [ai_usage_log] ([TenantId], [RunId], [Sequence]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_usage_log_TenantId_Status_CreatedAt] ON [ai_usage_log] ([TenantId], [Status], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260828041655_AddAiCenterP1', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [ai_provider_config] ADD [ComplianceConfirmedAt] datetimeoffset NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260828050053_AddAiProviderComplianceGate', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ai_document_draft] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [SourceInvocationId] nvarchar(128) NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [HandlerVersion] nvarchar(64) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [DraftVersion] int NOT NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [PayloadHash] nchar(64) NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [LastValidatedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_document_draft] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_document_draft_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_document_draft_ai_conversation_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [ai_conversation] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_document_draft_ai_run_RunId] FOREIGN KEY ([RunId]) REFERENCES [ai_run] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_document_draft_validation] (
    [Id] uniqueidentifier NOT NULL,
    [DraftId] uniqueidentifier NOT NULL,
    [DraftVersion] int NOT NULL,
    [PayloadHash] nchar(64) NOT NULL,
    [IsValid] bit NOT NULL,
    [ErrorsJson] nvarchar(max) NOT NULL,
    [ValidatedAt] datetimeoffset NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_document_draft_validation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_document_draft_validation_ai_document_draft_DraftId] FOREIGN KEY ([DraftId]) REFERENCES [ai_document_draft] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ai_document_draft_ActorUserId] ON [ai_document_draft] ([ActorUserId]);

CREATE INDEX [IX_ai_document_draft_ConversationId] ON [ai_document_draft] ([ConversationId]);

CREATE INDEX [IX_ai_document_draft_IsDeleted] ON [ai_document_draft] ([IsDeleted]);

CREATE INDEX [IX_ai_document_draft_RunId] ON [ai_document_draft] ([RunId]);

CREATE INDEX [IX_ai_document_draft_TenantId] ON [ai_document_draft] ([TenantId]);

CREATE INDEX [IX_ai_document_draft_TenantId_ActorUserId_Status_CreatedAt] ON [ai_document_draft] ([TenantId], [ActorUserId], [Status], [CreatedAt]);

CREATE INDEX [IX_ai_document_draft_TenantId_ConversationId_CreatedAt] ON [ai_document_draft] ([TenantId], [ConversationId], [CreatedAt]);

CREATE INDEX [IX_ai_document_draft_TenantId_RunId_CreatedAt] ON [ai_document_draft] ([TenantId], [RunId], [CreatedAt]);

CREATE UNIQUE INDEX [IX_ai_document_draft_TenantId_SourceInvocationId] ON [ai_document_draft] ([TenantId], [SourceInvocationId]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_document_draft_TenantId_Status_ExpiresAt] ON [ai_document_draft] ([TenantId], [Status], [ExpiresAt]);

CREATE INDEX [IX_ai_document_draft_validation_DraftId] ON [ai_document_draft_validation] ([DraftId]);

CREATE INDEX [IX_ai_document_draft_validation_IsDeleted] ON [ai_document_draft_validation] ([IsDeleted]);

CREATE INDEX [IX_ai_document_draft_validation_TenantId] ON [ai_document_draft_validation] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_document_draft_validation_TenantId_DraftId_DraftVersion] ON [ai_document_draft_validation] ([TenantId], [DraftId], [DraftVersion]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_document_draft_validation_TenantId_IsValid_ValidatedAt] ON [ai_document_draft_validation] ([TenantId], [IsValid], [ValidatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260828062206_AddAiDocumentDraftP2', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ai_document_confirmation] (
    [Id] uniqueidentifier NOT NULL,
    [DraftId] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [DraftVersion] int NOT NULL,
    [ConfirmationVersion] int NOT NULL,
    [PayloadHash] nchar(64) NOT NULL,
    [HandlerVersion] nvarchar(64) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [ConfirmedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [ConsumedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_document_confirmation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_document_confirmation_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_document_confirmation_ai_document_draft_DraftId] FOREIGN KEY ([DraftId]) REFERENCES [ai_document_draft] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_document_confirmation_ai_run_RunId] FOREIGN KEY ([RunId]) REFERENCES [ai_run] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_document_execution] (
    [Id] uniqueidentifier NOT NULL,
    [ConfirmationId] uniqueidentifier NOT NULL,
    [ConfirmationVersion] int NOT NULL,
    [DraftId] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [BusinessType] nvarchar(100) NOT NULL,
    [BusinessIdempotencyKey] nvarchar(160) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [BusinessEntityId] uniqueidentifier NULL,
    [BusinessNo] nvarchar(100) NULL,
    [BusinessStatus] nvarchar(32) NULL,
    [TraceId] nvarchar(128) NOT NULL,
    [OutboxMessageId] nvarchar(128) NULL,
    [ErrorCode] nvarchar(100) NULL,
    [ErrorSummary] nvarchar(1000) NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_document_execution] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_document_execution_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_document_execution_ai_document_confirmation_ConfirmationId] FOREIGN KEY ([ConfirmationId]) REFERENCES [ai_document_confirmation] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_document_execution_ai_document_draft_DraftId] FOREIGN KEY ([DraftId]) REFERENCES [ai_document_draft] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_document_execution_ai_run_RunId] FOREIGN KEY ([RunId]) REFERENCES [ai_run] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ai_document_confirmation_ActorUserId] ON [ai_document_confirmation] ([ActorUserId]);

CREATE INDEX [IX_ai_document_confirmation_DraftId] ON [ai_document_confirmation] ([DraftId]);

CREATE INDEX [IX_ai_document_confirmation_IsDeleted] ON [ai_document_confirmation] ([IsDeleted]);

CREATE INDEX [IX_ai_document_confirmation_RunId] ON [ai_document_confirmation] ([RunId]);

CREATE INDEX [IX_ai_document_confirmation_TenantId] ON [ai_document_confirmation] ([TenantId]);

CREATE INDEX [IX_ai_document_confirmation_TenantId_ActorUserId_Status_ExpiresAt] ON [ai_document_confirmation] ([TenantId], [ActorUserId], [Status], [ExpiresAt]);

CREATE UNIQUE INDEX [IX_ai_document_confirmation_TenantId_DraftId_DraftVersion] ON [ai_document_confirmation] ([TenantId], [DraftId], [DraftVersion]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_document_confirmation_TenantId_RunId_CreatedAt] ON [ai_document_confirmation] ([TenantId], [RunId], [CreatedAt]);

CREATE INDEX [IX_ai_document_execution_ActorUserId] ON [ai_document_execution] ([ActorUserId]);

CREATE INDEX [IX_ai_document_execution_ConfirmationId] ON [ai_document_execution] ([ConfirmationId]);

CREATE INDEX [IX_ai_document_execution_DraftId] ON [ai_document_execution] ([DraftId]);

CREATE INDEX [IX_ai_document_execution_IsDeleted] ON [ai_document_execution] ([IsDeleted]);

CREATE INDEX [IX_ai_document_execution_RunId] ON [ai_document_execution] ([RunId]);

CREATE INDEX [IX_ai_document_execution_TenantId] ON [ai_document_execution] ([TenantId]);

CREATE INDEX [IX_ai_document_execution_TenantId_BusinessEntityId_CreatedAt] ON [ai_document_execution] ([TenantId], [BusinessEntityId], [CreatedAt]);

CREATE UNIQUE INDEX [IX_ai_document_execution_TenantId_BusinessIdempotencyKey] ON [ai_document_execution] ([TenantId], [BusinessIdempotencyKey]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_ai_document_execution_TenantId_ConfirmationId_ConfirmationVersion] ON [ai_document_execution] ([TenantId], [ConfirmationId], [ConfirmationVersion]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_document_execution_TenantId_RunId_CreatedAt] ON [ai_document_execution] ([TenantId], [RunId], [CreatedAt]);

CREATE INDEX [IX_ai_document_execution_TenantId_Status_CreatedAt] ON [ai_document_execution] ([TenantId], [Status], [CreatedAt]);

CREATE INDEX [IX_ai_document_execution_TenantId_TraceId] ON [ai_document_execution] ([TenantId], [TraceId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260828065916_AddAiDocumentExecutionP3', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [mcp_client_binding] (
    [Id] uniqueidentifier NOT NULL,
    [ApiClientId] uniqueidentifier NOT NULL,
    [OAuthClientId] nvarchar(100) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_mcp_client_binding] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_mcp_client_binding_ApiClients_ApiClientId] FOREIGN KEY ([ApiClientId]) REFERENCES [ApiClients] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [mcp_dataset_definition] (
    [Id] uniqueidentifier NOT NULL,
    [DatasetCode] nvarchar(100) NOT NULL,
    [DatasetName] nvarchar(200) NOT NULL,
    [Version] nvarchar(64) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DataClassification] nvarchar(32) NOT NULL,
    [HandlerCode] nvarchar(100) NOT NULL,
    [MaxRows] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_mcp_dataset_definition] PRIMARY KEY ([Id])
);

CREATE TABLE [mcp_invocation_log] (
    [Id] uniqueidentifier NOT NULL,
    [ClientBindingId] uniqueidentifier NULL,
    [CallerType] nvarchar(32) NOT NULL,
    [ActorUserId] uniqueidentifier NULL,
    [OAuthClientId] nvarchar(100) NULL,
    [ToolName] nvarchar(100) NOT NULL,
    [DatasetCode] nvarchar(100) NULL,
    [TraceId] nvarchar(128) NOT NULL,
    [InputDigest] nvarchar(128) NOT NULL,
    [IpAddress] nvarchar(64) NULL,
    [Status] nvarchar(32) NOT NULL,
    [RowCount] int NOT NULL,
    [IsTruncated] bit NOT NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NOT NULL,
    [DurationMilliseconds] bigint NOT NULL,
    [ErrorCode] nvarchar(100) NULL,
    [ErrorSummary] nvarchar(1000) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_mcp_invocation_log] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_mcp_invocation_log_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_mcp_invocation_log_mcp_client_binding_ClientBindingId] FOREIGN KEY ([ClientBindingId]) REFERENCES [mcp_client_binding] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [mcp_client_dataset_grant] (
    [Id] uniqueidentifier NOT NULL,
    [ClientBindingId] uniqueidentifier NOT NULL,
    [DatasetId] uniqueidentifier NOT NULL,
    [AllowedFieldsJson] nvarchar(max) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_mcp_client_dataset_grant] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_mcp_client_dataset_grant_mcp_client_binding_ClientBindingId] FOREIGN KEY ([ClientBindingId]) REFERENCES [mcp_client_binding] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_mcp_client_dataset_grant_mcp_dataset_definition_DatasetId] FOREIGN KEY ([DatasetId]) REFERENCES [mcp_dataset_definition] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [mcp_dataset_field] (
    [Id] uniqueidentifier NOT NULL,
    [DatasetId] uniqueidentifier NOT NULL,
    [FieldCode] nvarchar(100) NOT NULL,
    [DisplayName] nvarchar(200) NOT NULL,
    [DataType] nvarchar(32) NOT NULL,
    [DataClassification] nvarchar(32) NOT NULL,
    [IsFilterable] bit NOT NULL,
    [IsDefault] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_mcp_dataset_field] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_mcp_dataset_field_mcp_dataset_definition_DatasetId] FOREIGN KEY ([DatasetId]) REFERENCES [mcp_dataset_definition] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_mcp_client_binding_ApiClientId] ON [mcp_client_binding] ([ApiClientId]);

CREATE INDEX [IX_mcp_client_binding_IsDeleted] ON [mcp_client_binding] ([IsDeleted]);

CREATE UNIQUE INDEX [IX_mcp_client_binding_OAuthClientId] ON [mcp_client_binding] ([OAuthClientId]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_mcp_client_binding_TenantId] ON [mcp_client_binding] ([TenantId]);

CREATE UNIQUE INDEX [IX_mcp_client_binding_TenantId_ApiClientId] ON [mcp_client_binding] ([TenantId], [ApiClientId]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_mcp_client_binding_TenantId_IsEnabled] ON [mcp_client_binding] ([TenantId], [IsEnabled]);

CREATE INDEX [IX_mcp_client_dataset_grant_ClientBindingId] ON [mcp_client_dataset_grant] ([ClientBindingId]);

CREATE INDEX [IX_mcp_client_dataset_grant_DatasetId] ON [mcp_client_dataset_grant] ([DatasetId]);

CREATE INDEX [IX_mcp_client_dataset_grant_IsDeleted] ON [mcp_client_dataset_grant] ([IsDeleted]);

CREATE INDEX [IX_mcp_client_dataset_grant_TenantId] ON [mcp_client_dataset_grant] ([TenantId]);

CREATE UNIQUE INDEX [IX_mcp_client_dataset_grant_TenantId_ClientBindingId_DatasetId] ON [mcp_client_dataset_grant] ([TenantId], [ClientBindingId], [DatasetId]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_mcp_client_dataset_grant_TenantId_ClientBindingId_IsEnabled] ON [mcp_client_dataset_grant] ([TenantId], [ClientBindingId], [IsEnabled]);

CREATE INDEX [IX_mcp_dataset_definition_IsDeleted] ON [mcp_dataset_definition] ([IsDeleted]);

CREATE INDEX [IX_mcp_dataset_definition_TenantId] ON [mcp_dataset_definition] ([TenantId]);

CREATE UNIQUE INDEX [IX_mcp_dataset_definition_TenantId_DatasetCode_Version] ON [mcp_dataset_definition] ([TenantId], [DatasetCode], [Version]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_mcp_dataset_definition_TenantId_IsEnabled_DatasetCode] ON [mcp_dataset_definition] ([TenantId], [IsEnabled], [DatasetCode]);

CREATE INDEX [IX_mcp_dataset_field_DatasetId] ON [mcp_dataset_field] ([DatasetId]);

CREATE INDEX [IX_mcp_dataset_field_IsDeleted] ON [mcp_dataset_field] ([IsDeleted]);

CREATE INDEX [IX_mcp_dataset_field_TenantId] ON [mcp_dataset_field] ([TenantId]);

CREATE UNIQUE INDEX [IX_mcp_dataset_field_TenantId_DatasetId_FieldCode] ON [mcp_dataset_field] ([TenantId], [DatasetId], [FieldCode]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_mcp_dataset_field_TenantId_DatasetId_IsDefault] ON [mcp_dataset_field] ([TenantId], [DatasetId], [IsDefault]);

CREATE INDEX [IX_mcp_invocation_log_ActorUserId] ON [mcp_invocation_log] ([ActorUserId]);

CREATE INDEX [IX_mcp_invocation_log_ClientBindingId] ON [mcp_invocation_log] ([ClientBindingId]);

CREATE INDEX [IX_mcp_invocation_log_IsDeleted] ON [mcp_invocation_log] ([IsDeleted]);

CREATE INDEX [IX_mcp_invocation_log_TenantId] ON [mcp_invocation_log] ([TenantId]);

CREATE INDEX [IX_mcp_invocation_log_TenantId_ClientBindingId_CreatedAt] ON [mcp_invocation_log] ([TenantId], [ClientBindingId], [CreatedAt]);

CREATE INDEX [IX_mcp_invocation_log_TenantId_DatasetCode_CreatedAt] ON [mcp_invocation_log] ([TenantId], [DatasetCode], [CreatedAt]);

CREATE INDEX [IX_mcp_invocation_log_TenantId_Status_CreatedAt] ON [mcp_invocation_log] ([TenantId], [Status], [CreatedAt]);

CREATE INDEX [IX_mcp_invocation_log_TenantId_TraceId] ON [mcp_invocation_log] ([TenantId], [TraceId]);

INSERT INTO [mcp_dataset_definition]
    ([Id], [DatasetCode], [DatasetName], [Version], [Description], [DataClassification], [HandlerCode], [MaxRows], [IsEnabled], [TenantId], [CreatedAt], [IsDeleted])
SELECT NEWID(), seed.[DatasetCode], seed.[DatasetName], N'1.0', seed.[Description], seed.[DataClassification], seed.[DatasetCode], seed.[MaxRows], 1, tenant.[Id], SYSUTCDATETIME(), 0
FROM [Tenants] AS tenant
CROSS JOIN (VALUES
    (N'platform-capabilities', N'Platform capabilities', N'Non-sensitive metadata describing enabled PermissionSystem capability families.', N'Public', 20),
    (N'department-directory', N'Department directory', N'Tenant-scoped department directory without internal identifiers or audit fields.', N'Internal', 100)
) AS seed ([DatasetCode], [DatasetName], [Description], [DataClassification], [MaxRows])
WHERE tenant.[IsDeleted] = 0
  AND NOT EXISTS (
      SELECT 1 FROM [mcp_dataset_definition] existing
      WHERE existing.[TenantId] = tenant.[Id]
        AND existing.[DatasetCode] = seed.[DatasetCode]
        AND existing.[Version] = N'1.0'
        AND existing.[IsDeleted] = 0);

INSERT INTO [mcp_dataset_field]
    ([Id], [DatasetId], [FieldCode], [DisplayName], [DataType], [DataClassification], [IsFilterable], [IsDefault], [TenantId], [CreatedAt], [IsDeleted])
SELECT NEWID(), dataset.[Id], seed.[FieldCode], seed.[DisplayName], seed.[DataType], seed.[DataClassification], seed.[IsFilterable], 1, dataset.[TenantId], SYSUTCDATETIME(), 0
FROM [mcp_dataset_definition] AS dataset
INNER JOIN (VALUES
    (N'platform-capabilities', N'code', N'Code', N'string', N'Public', 1),
    (N'platform-capabilities', N'name', N'Name', N'string', N'Public', 1),
    (N'platform-capabilities', N'status', N'Status', N'string', N'Public', 1),
    (N'department-directory', N'code', N'Department code', N'string', N'Internal', 1),
    (N'department-directory', N'name', N'Department name', N'string', N'Internal', 1),
    (N'department-directory', N'parentCode', N'Parent department code', N'string', N'Internal', 0),
    (N'department-directory', N'isEnabled', N'Enabled', N'boolean', N'Internal', 1)
) AS seed ([DatasetCode], [FieldCode], [DisplayName], [DataType], [DataClassification], [IsFilterable])
    ON seed.[DatasetCode] = dataset.[DatasetCode]
WHERE dataset.[Version] = N'1.0'
  AND dataset.[IsDeleted] = 0
  AND NOT EXISTS (
      SELECT 1 FROM [mcp_dataset_field] existing
      WHERE existing.[TenantId] = dataset.[TenantId]
        AND existing.[DatasetId] = dataset.[Id]
        AND existing.[FieldCode] = seed.[FieldCode]
        AND existing.[IsDeleted] = 0);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260828080505_AddMcpExternalAccessP4', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [ai_usage_log] ADD [Attempt] int NULL;

ALTER TABLE [ai_usage_log] ADD [InputTokenPricePerMillion] decimal(18,6) NULL;

ALTER TABLE [ai_usage_log] ADD [OutputTokenPricePerMillion] decimal(18,6) NULL;

ALTER TABLE [ai_usage_log] ADD [PricingCurrency] nvarchar(3) NULL;

ALTER TABLE [ai_usage_log] ADD [ReservationExpiresAt] datetimeoffset NULL;

ALTER TABLE [ai_usage_log] ADD [ReservedCost] decimal(18,6) NULL;

ALTER TABLE [ai_usage_log] ADD [Round] int NULL;

ALTER TABLE [ai_usage_log] ADD [RouteRole] nvarchar(32) NULL;

UPDATE [ai_usage_log] SET [Attempt] = 1, [Round] = [Sequence], [RouteRole] = N'Primary';

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ai_usage_log]') AND [c].[name] = N'Attempt');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [ai_usage_log] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [ai_usage_log] ALTER COLUMN [Attempt] int NOT NULL;

DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ai_usage_log]') AND [c].[name] = N'Round');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [ai_usage_log] DROP CONSTRAINT ' + @var7 + ';');
ALTER TABLE [ai_usage_log] ALTER COLUMN [Round] int NOT NULL;

DECLARE @var8 nvarchar(max);
SELECT @var8 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ai_usage_log]') AND [c].[name] = N'RouteRole');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [ai_usage_log] DROP CONSTRAINT ' + @var8 + ';');
ALTER TABLE [ai_usage_log] ALTER COLUMN [RouteRole] nvarchar(32) NOT NULL;

ALTER TABLE [ai_run] ADD [FallbackCount] int NOT NULL DEFAULT 0;

ALTER TABLE [ai_run] ADD [FinalProviderConfigId] uniqueidentifier NULL;

ALTER TABLE [ai_provider_config] ADD [InputTokenPricePerMillion] decimal(18,6) NULL;

ALTER TABLE [ai_provider_config] ADD [OutputTokenPricePerMillion] decimal(18,6) NULL;

ALTER TABLE [ai_provider_config] ADD [PricingCurrency] nvarchar(3) NULL;

ALTER TABLE [ai_provider_config] ADD [SupportsJsonSchema] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [ai_provider_config] ADD [SupportsTools] bit NOT NULL DEFAULT CAST(1 AS bit);

CREATE TABLE [ai_budget_policy] (
    [Id] uniqueidentifier NOT NULL,
    [PolicyCode] nvarchar(100) NOT NULL,
    [PolicyName] nvarchar(200) NOT NULL,
    [ScopeType] nvarchar(32) NOT NULL,
    [UserId] uniqueidentifier NULL,
    [MonthlyLimit] decimal(18,6) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [IsHardLimit] bit NOT NULL,
    [AlertThresholdPercentage] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_budget_policy] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_budget_policy_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_model_route_policy] (
    [Id] uniqueidentifier NOT NULL,
    [AgentCode] nvarchar(100) NOT NULL,
    [PrimaryProviderConfigId] uniqueidentifier NOT NULL,
    [CanaryProviderConfigId] uniqueidentifier NULL,
    [CanaryPercentage] int NOT NULL,
    [FallbackProviderConfigId] uniqueidentifier NULL,
    [IsEnabled] bit NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_model_route_policy] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_model_route_policy_ai_provider_config_CanaryProviderConfigId] FOREIGN KEY ([CanaryProviderConfigId]) REFERENCES [ai_provider_config] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_model_route_policy_ai_provider_config_FallbackProviderConfigId] FOREIGN KEY ([FallbackProviderConfigId]) REFERENCES [ai_provider_config] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_model_route_policy_ai_provider_config_PrimaryProviderConfigId] FOREIGN KEY ([PrimaryProviderConfigId]) REFERENCES [ai_provider_config] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_user_feedback] (
    [Id] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [MessageId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Rating] nvarchar(32) NOT NULL,
    [ReasonCode] nvarchar(64) NULL,
    [Comment] nvarchar(500) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_user_feedback] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_user_feedback_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_user_feedback_ai_message_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [ai_message] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_user_feedback_ai_run_RunId] FOREIGN KEY ([RunId]) REFERENCES [ai_run] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ai_usage_log_TenantId_PricingCurrency_CreatedAt] ON [ai_usage_log] ([TenantId], [PricingCurrency], [CreatedAt]);

CREATE INDEX [IX_ai_run_FinalProviderConfigId] ON [ai_run] ([FinalProviderConfigId]);

CREATE INDEX [IX_ai_run_TenantId_FinalProviderConfigId_CreatedAt] ON [ai_run] ([TenantId], [FinalProviderConfigId], [CreatedAt]);

CREATE INDEX [IX_ai_budget_policy_IsDeleted] ON [ai_budget_policy] ([IsDeleted]);

CREATE INDEX [IX_ai_budget_policy_TenantId] ON [ai_budget_policy] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_budget_policy_TenantId_PolicyCode] ON [ai_budget_policy] ([TenantId], [PolicyCode]) WHERE [IsDeleted] = 0;

CREATE UNIQUE INDEX [IX_ai_budget_policy_TenantId_ScopeType_UserId_Currency] ON [ai_budget_policy] ([TenantId], [ScopeType], [UserId], [Currency]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_budget_policy_TenantId_ScopeType_UserId_IsEnabled] ON [ai_budget_policy] ([TenantId], [ScopeType], [UserId], [IsEnabled]);

CREATE INDEX [IX_ai_budget_policy_UserId] ON [ai_budget_policy] ([UserId]);

CREATE INDEX [IX_ai_model_route_policy_CanaryProviderConfigId] ON [ai_model_route_policy] ([CanaryProviderConfigId]);

CREATE INDEX [IX_ai_model_route_policy_FallbackProviderConfigId] ON [ai_model_route_policy] ([FallbackProviderConfigId]);

CREATE INDEX [IX_ai_model_route_policy_IsDeleted] ON [ai_model_route_policy] ([IsDeleted]);

CREATE INDEX [IX_ai_model_route_policy_PrimaryProviderConfigId] ON [ai_model_route_policy] ([PrimaryProviderConfigId]);

CREATE INDEX [IX_ai_model_route_policy_TenantId] ON [ai_model_route_policy] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_model_route_policy_TenantId_AgentCode] ON [ai_model_route_policy] ([TenantId], [AgentCode]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_model_route_policy_TenantId_IsEnabled] ON [ai_model_route_policy] ([TenantId], [IsEnabled]);

CREATE INDEX [IX_ai_user_feedback_IsDeleted] ON [ai_user_feedback] ([IsDeleted]);

CREATE INDEX [IX_ai_user_feedback_MessageId] ON [ai_user_feedback] ([MessageId]);

CREATE INDEX [IX_ai_user_feedback_RunId] ON [ai_user_feedback] ([RunId]);

CREATE INDEX [IX_ai_user_feedback_TenantId] ON [ai_user_feedback] ([TenantId]);

CREATE INDEX [IX_ai_user_feedback_TenantId_Rating_CreatedAt] ON [ai_user_feedback] ([TenantId], [Rating], [CreatedAt]);

CREATE UNIQUE INDEX [IX_ai_user_feedback_TenantId_RunId_UserId] ON [ai_user_feedback] ([TenantId], [RunId], [UserId]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_user_feedback_UserId] ON [ai_user_feedback] ([UserId]);

ALTER TABLE [ai_run] ADD CONSTRAINT [FK_ai_run_ai_provider_config_FinalProviderConfigId] FOREIGN KEY ([FinalProviderConfigId]) REFERENCES [ai_provider_config] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260828091058_AddAiEnterpriseOperationsP5A', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [ai_run] ADD [DeadlineAt] datetimeoffset NULL;

ALTER TABLE [ai_run] ADD [ExecutionLeaseId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE [ai_run] ADD [LastHeartbeatAt] datetimeoffset NULL;

ALTER TABLE [ai_run] ADD [RetryOfRunId] uniqueidentifier NULL;

CREATE INDEX [IX_ai_run_RetryOfRunId] ON [ai_run] ([RetryOfRunId]);

CREATE INDEX [IX_ai_run_TenantId_RetryOfRunId] ON [ai_run] ([TenantId], [RetryOfRunId]);

CREATE INDEX [IX_ai_run_TenantId_Status_LastHeartbeatAt] ON [ai_run] ([TenantId], [Status], [LastHeartbeatAt]);

ALTER TABLE [ai_run] ADD CONSTRAINT [FK_ai_run_ai_run_RetryOfRunId] FOREIGN KEY ([RetryOfRunId]) REFERENCES [ai_run] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260829004924_AddAiRunGovernance', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [mcp_dataset_definition] ADD [PublicationStatus] nvarchar(32) NOT NULL DEFAULT N'Draft';

ALTER TABLE [mcp_dataset_definition] ADD [PublishedAt] datetimeoffset NULL;

ALTER TABLE [mcp_dataset_definition] ADD [SchemaHash] nvarchar(64) NOT NULL DEFAULT N'';

ALTER TABLE [mcp_client_dataset_grant] ADD [ApprovedSchemaHash] nvarchar(64) NOT NULL DEFAULT N'';

UPDATE dataset
SET dataset.[SchemaHash] = CASE dataset.[DatasetCode]
        WHEN N'platform-capabilities' THEN N'B9DCA44A8861B0327C5185CCE989DFC5B8234C57270BA1077AAEF73EA0FEE6C2'
        WHEN N'department-directory' THEN N'716DF9CB29D081721687E2420E981DB950CE82E7F8E262B2331FF7E489A4EDD0'
    END,
    dataset.[PublicationStatus] = N'Published',
    dataset.[PublishedAt] = COALESCE(dataset.[PublishedAt], SYSUTCDATETIME())
FROM [mcp_dataset_definition] AS dataset
WHERE dataset.[Version] = N'1.0'
  AND dataset.[IsDeleted] = 0
  AND dataset.[DatasetCode] IN (N'platform-capabilities', N'department-directory');

UPDATE grantRow
SET grantRow.[ApprovedSchemaHash] = dataset.[SchemaHash]
FROM [mcp_client_dataset_grant] AS grantRow
INNER JOIN [mcp_dataset_definition] AS dataset
    ON dataset.[Id] = grantRow.[DatasetId]
   AND dataset.[TenantId] = grantRow.[TenantId]
WHERE grantRow.[IsDeleted] = 0
  AND dataset.[IsDeleted] = 0
  AND LEN(dataset.[SchemaHash]) = 64;

CREATE INDEX [IX_mcp_dataset_definition_TenantId_PublicationStatus_IsEnabled_DatasetCode] ON [mcp_dataset_definition] ([TenantId], [PublicationStatus], [IsEnabled], [DatasetCode]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260831092535_AddMcpDatasetSchemaGovernanceP4', N'10.0.10');

COMMIT;
GO

