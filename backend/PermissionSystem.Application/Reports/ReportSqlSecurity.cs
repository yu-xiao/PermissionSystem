using System.Text.RegularExpressions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Reports;

public static partial class ReportSqlSecurity
{
    public static string ValidateSelectSql(string? sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "SQL text is required.");
        }

        var sql = sqlText.Trim();
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Only SELECT SQL is allowed.");
        }

        if (sql.Contains(';') || sql.Contains("--", StringComparison.Ordinal) || sql.Contains("/*", StringComparison.Ordinal) || sql.Contains("*/", StringComparison.Ordinal))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "SQL must be a single SELECT statement without comments.");
        }

        if (ForbiddenKeywordRegex().IsMatch(sql))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "SQL contains forbidden keywords.");
        }

        return sql;
    }

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|EXEC|EXECUTE|MERGE|TRUNCATE|CREATE|GRANT|REVOKE|DENY|USE|BACKUP|RESTORE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ForbiddenKeywordRegex();
}
