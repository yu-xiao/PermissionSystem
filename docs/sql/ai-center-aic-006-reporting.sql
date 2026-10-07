/* AIC-006 deployment review script. Execute only in the explicitly approved
   database after DBA review. No EF migration, business data or existing view
   is changed. The report connection continues to use the existing isolated
   read-only role; do not grant dbo/schema-wide SELECT or use the API login.
   OPENJSON requires database compatibility level >= 130. */
SET XACT_ABORT ON;
IF SCHEMA_ID(N'reporting') IS NULL
    THROW 51000, 'The reviewed EA-016 reporting schema must already exist.', 1;
IF DATABASE_PRINCIPAL_ID(N'PermissionSystemReportReader') IS NULL
    THROW 51000, 'The reviewed isolated report reader role must already exist.', 1;
IF (SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) < 130
    THROW 51000, 'AIC-006 requires database compatibility level 130 or greater.', 1;
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL OR
   COL_LENGTH(N'dbo.Users', N'Id') IS NULL OR
   COL_LENGTH(N'dbo.Users', N'TenantId') IS NULL OR
   COL_LENGTH(N'dbo.Users', N'DepartmentId') IS NULL OR
   COL_LENGTH(N'dbo.Users', N'UserName') IS NULL OR
   COL_LENGTH(N'dbo.Users', N'DisplayName') IS NULL OR
   COL_LENGTH(N'dbo.Users', N'IsEnabled') IS NULL OR
   COL_LENGTH(N'dbo.Users', N'CreatedAt') IS NULL OR
   COL_LENGTH(N'dbo.Users', N'IsDeleted') IS NULL
    THROW 51000, 'The expected Users schema is unavailable.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    EXEC(N'CREATE OR ALTER VIEW [reporting].[AiSystemUsers]
        AS SELECT [Id], [TenantId], [DepartmentId], [UserName], [DisplayName],
                  [IsEnabled], [CreatedAt]
           FROM [dbo].[Users] WHERE [IsDeleted] = 0;');
    GRANT SELECT ON OBJECT::[reporting].[AiSystemUsers] TO [PermissionSystemReportReader];
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* Keep EnableReportDatasetTool=false and ApprovedReportDatasetKeys empty
   until identity, row scope, metrics, readonly credentials and Owner checks
   have passed. Explicitly approve system-users-scoped and create a report
   definition through the existing tenant report administration entry point.
   Rollback starts by disabling the tool/approval; leave the added view and
   historical messages in place. No automatic DROP or data cleanup. */
