namespace PermissionSystem.Domain.Enums;

public enum DataScopeType
{
    All = 0,
    CurrentUser = 1,
    CurrentDepartment = 2,
    CurrentDepartmentAndChildren = 3,
    CustomDepartments = 4
}
