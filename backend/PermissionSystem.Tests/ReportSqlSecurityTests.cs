using PermissionSystem.Application.Reports;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Tests;

public sealed class ReportSqlSecurityTests
{
    [Fact]
    public void ValidateSelectSql_ShouldAllowSelect()
    {
        var sql = ReportSqlSecurity.ValidateSelectSql("SELECT TenantId, UserName FROM Users");

        Assert.Equal("SELECT TenantId, UserName FROM Users", sql);
    }

    [Theory]
    [InlineData("UPDATE Users SET UserName = 'x'")]
    [InlineData("SELECT * FROM Users; DROP TABLE Users")]
    [InlineData("SELECT * FROM Users -- comment")]
    [InlineData("EXEC sp_who")]
    public void ValidateSelectSql_ShouldRejectUnsafeSql(string sql)
    {
        Assert.Throws<BusinessException>(() => ReportSqlSecurity.ValidateSelectSql(sql));
    }
}
