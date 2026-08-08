using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class ReportExecutionLog : BaseEntity
{
    public Guid ReportId { get; set; }

    public string ReportCode { get; set; } = string.Empty;

    public Guid? ExecuteUserId { get; set; }

    public string? ExecuteUserName { get; set; }

    public string? ParamsJson { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public int RowCount { get; set; }

    public bool IsSuccess { get; set; }

    public string? FailureReason { get; set; }
}
