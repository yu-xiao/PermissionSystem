/*
EA-006 tenant consistency audit (read-only)

Purpose:
  Report historical records whose relationship crosses tenant boundaries.

Safety:
  This script only executes SELECT statements. It does not update or delete data.
  Review every result with the owning business team before preparing any repair script.
*/

SET NOCOUNT ON;

SELECT
    'Tenants.TenantId' AS CheckName,
    CONVERT(nvarchar(36), tenant.Id) AS RecordId,
    tenant.TenantId AS RecordTenantId,
    tenant.Id AS RelatedTenantId,
    'TenantId must equal Id' AS Problem
FROM dbo.Tenants AS tenant
WHERE tenant.TenantId <> tenant.Id;

SELECT
    'Users.DepartmentId' AS CheckName,
    CONVERT(nvarchar(36), userEntity.Id) AS RecordId,
    userEntity.TenantId AS RecordTenantId,
    department.TenantId AS RelatedTenantId,
    'User and department belong to different tenants' AS Problem
FROM dbo.Users AS userEntity
INNER JOIN dbo.Departments AS department ON department.Id = userEntity.DepartmentId
WHERE userEntity.TenantId <> department.TenantId;

SELECT
    'Departments.ParentId' AS CheckName,
    CONVERT(nvarchar(36), department.Id) AS RecordId,
    department.TenantId AS RecordTenantId,
    parentDepartment.TenantId AS RelatedTenantId,
    'Department and parent department belong to different tenants' AS Problem
FROM dbo.Departments AS department
INNER JOIN dbo.Departments AS parentDepartment ON parentDepartment.Id = department.ParentId
WHERE department.TenantId <> parentDepartment.TenantId;

SELECT
    'UserRoles' AS CheckName,
    CONVERT(nvarchar(36), userRole.Id) AS RecordId,
    userRole.TenantId AS RecordTenantId,
    COALESCE(userEntity.TenantId, role.TenantId) AS RelatedTenantId,
    'UserRole, user and role tenant ids are inconsistent' AS Problem
FROM dbo.UserRoles AS userRole
INNER JOIN dbo.Users AS userEntity ON userEntity.Id = userRole.UserId
INNER JOIN dbo.Roles AS role ON role.Id = userRole.RoleId
WHERE userRole.TenantId <> userEntity.TenantId
   OR userRole.TenantId <> role.TenantId
   OR userEntity.TenantId <> role.TenantId;

SELECT
    'RoleMenus' AS CheckName,
    CONVERT(nvarchar(36), roleMenu.Id) AS RecordId,
    roleMenu.TenantId AS RecordTenantId,
    COALESCE(role.TenantId, menu.TenantId) AS RelatedTenantId,
    'RoleMenu, role and menu tenant ids are inconsistent' AS Problem
FROM dbo.RoleMenus AS roleMenu
INNER JOIN dbo.Roles AS role ON role.Id = roleMenu.RoleId
INNER JOIN dbo.Menus AS menu ON menu.Id = roleMenu.MenuId
WHERE roleMenu.TenantId <> role.TenantId
   OR roleMenu.TenantId <> menu.TenantId
   OR role.TenantId <> menu.TenantId;

SELECT
    'RolePermissions' AS CheckName,
    CONVERT(nvarchar(36), rolePermission.Id) AS RecordId,
    rolePermission.TenantId AS RecordTenantId,
    COALESCE(role.TenantId, permission.TenantId) AS RelatedTenantId,
    'RolePermission, role and permission tenant ids are inconsistent' AS Problem
FROM dbo.RolePermissions AS rolePermission
INNER JOIN dbo.Roles AS role ON role.Id = rolePermission.RoleId
INNER JOIN dbo.Permissions AS permission ON permission.Id = rolePermission.PermissionId
WHERE rolePermission.TenantId <> role.TenantId
   OR rolePermission.TenantId <> permission.TenantId
   OR role.TenantId <> permission.TenantId;

SELECT
    'DictionaryItems.TypeCode' AS CheckName,
    CONVERT(nvarchar(36), item.Id) AS RecordId,
    item.TenantId AS RecordTenantId,
    NULL AS RelatedTenantId,
    'No dictionary type with the same TenantId and TypeCode exists' AS Problem
FROM dbo.DictionaryItems AS item
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.DictionaryTypes AS dictionaryType
    WHERE dictionaryType.TenantId = item.TenantId
      AND dictionaryType.Code = item.TypeCode
);

SELECT
    'wf_business_binding.DefinitionId' AS CheckName,
    CONVERT(nvarchar(36), binding.Id) AS RecordId,
    binding.TenantId AS RecordTenantId,
    definition.TenantId AS RelatedTenantId,
    'Workflow binding and definition belong to different tenants' AS Problem
FROM dbo.wf_business_binding AS binding
INNER JOIN dbo.wf_definition AS definition ON definition.Id = binding.DefinitionId
WHERE binding.TenantId <> definition.TenantId;
