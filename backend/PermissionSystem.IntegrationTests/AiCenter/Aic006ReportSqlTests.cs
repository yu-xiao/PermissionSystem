using System.Text.Json;
using Microsoft.Data.SqlClient;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Reports;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Reports;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic006ReportSqlTests
{
    private const string ConnectionEnvironment = "PERMISSION_SYSTEM_AIC006_SQL_TEST_CONNECTION";

    [Aic006SqlFact]
    [Trait("Category", "SqlServer")]
    public async Task ParameterizedQueries_MatchExistingScopeFilterAndFullPopulationCounts()
    {
        // Opt-in isolated SQL semantics only: one session-local table, no migrations, views or persistent rows.
        await using var connection = new SqlConnection(Environment.GetEnvironmentVariable(ConnectionEnvironment));
        await connection.OpenAsync();
        var tenant = Guid.NewGuid(); var otherTenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var firstDepartment = Guid.NewGuid(); var secondDepartment = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-09-01T00:00:00+08:00"); var end = start.AddMonths(1);
        var users = Enumerable.Range(0, 303).Select(index => new User
        {
            Id = index == 0 ? actor : Guid.NewGuid(), TenantId = index == 302 ? otherTenant : tenant,
            DepartmentId = index % 3 == 0 ? null : index % 3 == 1 ? firstDepartment : secondDepartment,
            IsEnabled = index % 2 == 0, IsDeleted = index == 301,
            UserName = $"user-{index:000}", DisplayName = "测试用户", CreatedAt = index == 300 ? end : start
        }).ToArray();
        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "CREATE TABLE #Aic006Users ([Id] uniqueidentifier, [TenantId] uniqueidentifier, [DepartmentId] uniqueidentifier NULL, " +
                "[UserName] nvarchar(64), [DisplayName] nvarchar(128), [IsEnabled] bit, [CreatedAt] datetimeoffset, [IsDeleted] bit)";
            await setup.ExecuteNonQueryAsync();
        }
        foreach (var user in users)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO #Aic006Users VALUES (@id,@tenant,@department,@name,@display,@enabled,@created,@deleted)";
            insert.Parameters.AddWithValue("id", user.Id); insert.Parameters.AddWithValue("tenant", user.TenantId);
            insert.Parameters.AddWithValue("department", (object?)user.DepartmentId ?? DBNull.Value);
            insert.Parameters.AddWithValue("name", user.UserName); insert.Parameters.AddWithValue("display", user.DisplayName);
            insert.Parameters.AddWithValue("enabled", user.IsEnabled); insert.Parameters.AddWithValue("created", user.CreatedAt);
            insert.Parameters.AddWithValue("deleted", user.IsDeleted); await insert.ExecuteNonQueryAsync();
        }
        DataScopeContext[] scopes =
        [
            new() { ScopeType = DataScopeType.All, CurrentUserId = actor },
            new() { ScopeType = DataScopeType.CurrentUser, CurrentUserId = actor },
            new() { ScopeType = DataScopeType.CurrentDepartment, CurrentUserId = actor, DepartmentIds = [firstDepartment] },
            new() { ScopeType = DataScopeType.CurrentDepartmentAndChildren, CurrentUserId = actor, DepartmentIds = [firstDepartment, secondDepartment] },
            new() { ScopeType = DataScopeType.CustomDepartments, CurrentUserId = actor, IncludeCurrentUser = true, DepartmentIds = [firstDepartment] },
            new() { ScopeType = DataScopeType.CustomDepartments, CurrentUserId = actor }
        ];
        foreach (var scope in scopes)
        {
            foreach (var onlyEnabled in new bool?[] { null, true })
            {
                var baseline = new DataPermissionFilter().Apply(users.Where(item => item.TenantId == tenant && !item.IsDeleted &&
                    item.CreatedAt >= start && item.CreatedAt < end && (!onlyEnabled.HasValue || item.IsEnabled == onlyEnabled.Value)).AsQueryable(),
                    scope, item => (Guid?)item.Id, item => item.DepartmentId).ToArray();
                foreach (var dimension in new[] { "None", "DepartmentId", "IsEnabled" })
                {
                    await using var command = connection.CreateCommand();
                    ControlledUserReportSql.Configure(command, new()
                    {
                        Context = new() { TenantId = tenant, ActorUserId = actor, Scope = scope },
                        UserFilters = new() { StartTime = start, EndTime = end, IsEnabled = onlyEnabled },
                        Mode = "Metrics", Dimension = dimension, Limit = 1
                    }, 200);
                    UseSessionSource(command);
                    await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
                    Assert.Equal(baseline.LongLength, reader.GetInt64(0));
                    Assert.Equal(baseline.LongCount(item => item.IsEnabled), reader.GetInt64(1));
                    Assert.Equal(baseline.LongCount(item => !item.IsEnabled), reader.GetInt64(2));
                    var groups = JsonSerializer.Deserialize<List<ReportUserMetricGroup>>(reader.GetString(4), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                    Assert.True(groups.Count <= 1);
                    var expectedGroups = dimension == "None" ? 0 : dimension == "DepartmentId"
                        ? baseline.Select(item => item.DepartmentId).Distinct().Count() : baseline.Select(item => item.IsEnabled).Distinct().Count();
                    Assert.Equal(expectedGroups, reader.GetInt64(3));
                    if (groups.Count > 0)
                    {
                        var group = groups[0];
                        Guid? groupDepartmentId = dimension == "DepartmentId" && group.Key is not null ? Guid.Parse(group.Key) : null;
                        var expected = dimension == "DepartmentId"
                            ? baseline.Where(item => item.DepartmentId == groupDepartmentId).ToArray()
                            : baseline.Where(item => item.IsEnabled.ToString().ToLowerInvariant() == group.Key).ToArray();
                        Assert.Equal(expected.LongLength, group.Values.UserCount);
                        Assert.Equal(expected.LongCount(item => item.IsEnabled), group.Values.EnabledUserCount);
                    }
                }
                await using var detail = connection.CreateCommand();
                ControlledUserReportSql.Configure(detail, new()
                {
                    Context = new() { TenantId = tenant, ActorUserId = actor, Scope = scope },
                    UserFilters = new() { StartTime = start, EndTime = end, IsEnabled = onlyEnabled }, Limit = 2
                }, 200);
                UseSessionSource(detail);
                await using var detailReader = await detail.ExecuteReaderAsync(); Assert.True(await detailReader.ReadAsync());
                Assert.Equal(baseline.LongLength, detailReader.GetInt64(0));
                using var rows = JsonDocument.Parse(detailReader.GetString(1));
                var ids = rows.RootElement.EnumerateArray().Select(item => item.GetProperty("Id").GetGuid()).ToArray();
                Assert.Equal(baseline.OrderBy(item => item.UserName).ThenBy(item => item.Id).Take(2).Select(item => item.Id), ids);
            }
        }
    }

    private static void UseSessionSource(SqlCommand command) => command.CommandText = command.CommandText.Replace(
        "[reporting].[AiSystemUsers]", "(SELECT * FROM #Aic006Users WHERE [IsDeleted] = 0)", StringComparison.Ordinal);

    private sealed class Aic006SqlFactAttribute : FactAttribute
    {
        public Aic006SqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvironment)))
                Skip = "An explicitly isolated AIC-006 SQL Server connection is required; no database was selected automatically.";
        }
    }
}
