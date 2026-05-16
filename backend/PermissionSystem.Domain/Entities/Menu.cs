using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class Menu : BaseEntity
{
    public Guid? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Path { get; set; }

    public string? Component { get; set; }

    public string? Redirect { get; set; }

    public string? Icon { get; set; }

    public int Sort { get; set; }

    public bool Visible { get; set; } = true;

    public bool KeepAlive { get; set; }

    public string MenuType { get; set; } = "Menu";

    public string? PermissionCode { get; set; }

    public Menu? Parent { get; set; }

    public ICollection<Menu> Children { get; set; } = [];

    public ICollection<RoleMenu> RoleMenus { get; set; } = [];
}
