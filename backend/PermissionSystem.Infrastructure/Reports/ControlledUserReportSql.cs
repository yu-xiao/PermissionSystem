using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using PermissionSystem.Application.Reports;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Reports;

public static class ControlledUserReportSql
{
    public static void Configure(SqlCommand command, ReportExecutionRequest request, int maximumRows)
    {
        var context = request.Context ?? throw new BusinessException(ErrorCode.Forbidden, "A server report context is required.");
        if (context.TenantId == Guid.Empty || context.ActorUserId == Guid.Empty || context.Scope.CurrentUserId != context.ActorUserId ||
            context.Scope.DepartmentIds.Count > ReportDatasetCapabilities.MaxDepartmentIds)
            throw new BusinessException(ErrorCode.Forbidden, "Invalid report scope context.");
        var filters = request.UserFilters ?? throw new BusinessException(ErrorCode.ValidationFailed, "Controlled report filters are required.");
        ReportDatasetCapabilities.ValidateQuery(request.Mode, request.Dimension, request.Sort, request.Limit);
        var maximum = Math.Clamp(maximumRows, 1, 10000);
        var limit = Math.Min(request.Limit ?? maximum, maximum);
        command.Parameters.Add("__TenantId", SqlDbType.UniqueIdentifier).Value = context.TenantId;
        command.Parameters.Add("__MaxRows", SqlDbType.Int).Value = limit;
        var predicates = new List<string> { "source.[TenantId] = @__TenantId" };
        if (!context.Scope.HasAllDataScope)
        {
            var scopeParts = new List<string>();
            if (context.Scope.IncludesCurrentUser)
            {
                command.Parameters.Add("__ActorId", SqlDbType.UniqueIdentifier).Value = context.ActorUserId;
                scopeParts.Add("source.[Id] = @__ActorId");
            }
            var departments = context.Scope.DepartmentIds.Distinct().Order().ToArray();
            if (departments.Length > 0)
            {
                command.Parameters.Add("__Departments", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(departments);
                scopeParts.Add("source.[DepartmentId] IN (SELECT [Id] FROM OPENJSON(@__Departments) WITH ([Id] uniqueidentifier '$'))");
            }
            predicates.Add(scopeParts.Count == 0 ? "1 = 0" : $"({string.Join(" OR ", scopeParts)})");
        }
        if (filters.DepartmentScope == "CurrentDepartment")
        {
            if (context.Scope.CurrentDepartmentId is not Guid currentDepartment || currentDepartment == Guid.Empty)
                throw new BusinessException(ErrorCode.ValidationFailed, "A current department is required.");
            command.Parameters.Add("__CurrentDepartment", SqlDbType.UniqueIdentifier).Value = currentDepartment;
            predicates.Add("source.[DepartmentId] = @__CurrentDepartment");
        }
        if (filters.DepartmentId.HasValue)
        {
            command.Parameters.Add("__Department", SqlDbType.UniqueIdentifier).Value = filters.DepartmentId.Value;
            predicates.Add("source.[DepartmentId] = @__Department");
        }
        if (filters.Keyword is not null)
        {
            var escaped = filters.Keyword.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");
            command.Parameters.Add("__Keyword", SqlDbType.NVarChar, 202).Value = $"%{escaped}%";
            predicates.Add("(source.[UserName] LIKE @__Keyword ESCAPE N'~' OR source.[DisplayName] LIKE @__Keyword ESCAPE N'~')");
        }
        if (filters.IsEnabled.HasValue)
        {
            command.Parameters.Add("__Enabled", SqlDbType.Bit).Value = filters.IsEnabled.Value;
            predicates.Add("source.[IsEnabled] = @__Enabled");
        }
        if (filters.StartTime.HasValue)
        {
            command.Parameters.Add("__Start", SqlDbType.DateTimeOffset).Value = filters.StartTime.Value;
            predicates.Add("source.[CreatedAt] >= @__Start");
        }
        if (filters.EndTime.HasValue)
        {
            command.Parameters.Add("__End", SqlDbType.DateTimeOffset).Value = filters.EndTime.Value;
            predicates.Add("source.[CreatedAt] < @__End");
        }
        var source = "WITH filtered AS (SELECT source.[Id], source.[DepartmentId], source.[UserName], source.[DisplayName], " +
            "source.[IsEnabled], source.[CreatedAt] FROM [reporting].[AiSystemUsers] AS source WHERE " + string.Join(" AND ", predicates) + ")";
        if (request.Mode == "Rows")
        {
            command.CommandText = source + " SELECT (SELECT COUNT_BIG(*) FROM filtered) AS TotalCount, " +
                "(SELECT TOP (@__MaxRows) [Id], [DepartmentId], [UserName], [DisplayName], [IsEnabled], [CreatedAt] " +
                "FROM filtered ORDER BY [UserName], [Id] FOR JSON PATH, INCLUDE_NULL_VALUES) AS RowsJson";
            return;
        }
        const string counts = "COUNT_BIG(*) AS UserCount, COALESCE(SUM(CAST(CASE WHEN [IsEnabled] = 1 THEN 1 ELSE 0 END AS bigint)), 0) AS EnabledUserCount, " +
            "COALESCE(SUM(CAST(CASE WHEN [IsEnabled] = 0 THEN 1 ELSE 0 END AS bigint)), 0) AS DisabledUserCount";
        if (request.Dimension == "None")
        {
            command.CommandText = source + " SELECT " + counts + ", CAST(0 AS bigint) AS TotalGroupCount, N'[]' AS GroupsJson FROM filtered";
            return;
        }
        var key = request.Dimension == "DepartmentId" ? "CONVERT(nvarchar(36), [DepartmentId])" :
            "CASE WHEN [IsEnabled] = 1 THEN N'true' ELSE N'false' END";
        var groupColumn = request.Dimension == "DepartmentId" ? "[DepartmentId]" : "[IsEnabled]";
        var order = request.Dimension == "DepartmentId" ? "[UserCount] DESC, CASE WHEN [Key] IS NULL THEN 0 ELSE 1 END, [Key]" : "[Key]";
        command.CommandText = source + $", grouped AS (SELECT {key} AS [Key], {counts} FROM filtered GROUP BY {groupColumn}) " +
            "SELECT " + counts + ", (SELECT COUNT_BIG(*) FROM grouped) AS TotalGroupCount, " +
            "(SELECT TOP (@__MaxRows) [Key], [UserCount] AS [Values.UserCount], [EnabledUserCount] AS [Values.EnabledUserCount], " +
            $"[DisabledUserCount] AS [Values.DisabledUserCount] FROM grouped ORDER BY {order} FOR JSON PATH, INCLUDE_NULL_VALUES) AS GroupsJson FROM filtered";
    }
}
