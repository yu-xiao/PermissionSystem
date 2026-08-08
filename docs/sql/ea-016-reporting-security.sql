/*
   EA-016 reporting database objects. Run this script as a DBA account after
   applying the EF migration, then create a dedicated login/user and add it to
   PermissionSystemReportReader. Do not grant this user db_datareader, db_owner,
   or any permission on dbo, OpenIddict, or Hangfire objects.
*/

IF SCHEMA_ID(N'reporting') IS NULL
    EXEC(N'CREATE SCHEMA [reporting] AUTHORIZATION [dbo]');
GO

CREATE OR ALTER VIEW [reporting].[SystemUsers]
AS
SELECT
    [TenantId],
    [UserName],
    [DisplayName],
    [Email],
    [PhoneNumber],
    [IsEnabled],
    [CreatedAt]
FROM [dbo].[Users]
WHERE [IsDeleted] = 0;
GO

CREATE OR ALTER VIEW [reporting].[SystemLoginLogs]
AS
SELECT
    [TenantId],
    [UserName],
    [LoginType],
    [IpAddress],
    [LoginResult],
    [FailureReason],
    [CreatedAt]
FROM [dbo].[LoginLogs]
WHERE [IsDeleted] = 0;
GO

CREATE OR ALTER VIEW [reporting].[SystemOperationLogs]
AS
SELECT
    [TenantId],
    [UserName],
    [Module],
    [Action],
    [RequestMethod],
    [StatusCode],
    [ElapsedMilliseconds],
    [CreatedAt]
FROM [dbo].[OperationLogs]
WHERE [IsDeleted] = 0;
GO

IF DATABASE_PRINCIPAL_ID(N'PermissionSystemReportReader') IS NULL
    CREATE ROLE [PermissionSystemReportReader];
GO

GRANT SELECT ON OBJECT::[reporting].[SystemUsers] TO [PermissionSystemReportReader];
GRANT SELECT ON OBJECT::[reporting].[SystemLoginLogs] TO [PermissionSystemReportReader];
GRANT SELECT ON OBJECT::[reporting].[SystemOperationLogs] TO [PermissionSystemReportReader];
GO

/*
   DBA-owned deployment steps, intentionally without a password value:

   CREATE LOGIN [permission_report_reader] WITH PASSWORD = '<stored-secret>';
   USE [PermissionSystemDb];
   CREATE USER [permission_report_reader] FOR LOGIN [permission_report_reader];
   ALTER ROLE [PermissionSystemReportReader] ADD MEMBER [permission_report_reader];

   Configure the resulting connection only through the deployment secret store:
   Reports__ReportConnection=<dedicated read-only connection string>
   Reports__SqlReportsEnabled=true
*/
