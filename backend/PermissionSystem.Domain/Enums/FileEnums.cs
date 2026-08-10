namespace PermissionSystem.Domain.Enums;

public enum FileStatus
{
    Pending = 0,
    Active = 1,
    PendingDelete = 2,
    Deleted = 3,
    Failed = 4
}

public enum FileScanStatus
{
    Pending = 0,
    Clean = 1,
    Infected = 2,
    Failed = 3
}
