using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Menus;

public sealed class MenuService : IMenuService
{
    private readonly IRepository<Menu> _menuRepository;
    private readonly IRepository<RoleMenu> _roleMenuRepository;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;

    public MenuService(
        IRepository<Menu> menuRepository,
        IRepository<RoleMenu> roleMenuRepository,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork)
    {
        _menuRepository = menuRepository;
        _roleMenuRepository = roleMenuRepository;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<MenuTreeResponse>> GetTreeAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var menus = _menuRepository.Query()
            .Where(entity => !tenantId.HasValue || entity.TenantId == tenantId.Value)
            .OrderBy(entity => entity.Sort)
            .ToList();

        return Task.FromResult(BuildTree(menus));
    }

    public async Task<MenuTreeResponse> CreateAsync(CreateMenuRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.Name, "Menu name is required.");
        var tenantId = _tenantWriteResolver.ResolveTenantId(request.TenantId);

        if (request.ParentId.HasValue)
        {
            var parent = await _menuRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent is null || parent.TenantId != tenantId)
            {
                throw new BusinessException(ErrorCode.BadRequest, "Parent menu is invalid.");
            }
        }

        var menu = new Menu
        {
            TenantId = tenantId,
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            Path = request.Path,
            Component = request.Component,
            Redirect = request.Redirect,
            Icon = request.Icon,
            Sort = request.Sort,
            Visible = request.Visible,
            KeepAlive = request.KeepAlive,
            MenuType = request.MenuType,
            PermissionCode = request.PermissionCode
        };

        await _menuRepository.AddAsync(menu, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(menu);
    }

    public async Task<MenuTreeResponse> UpdateAsync(Guid id, UpdateMenuRequest request, CancellationToken cancellationToken = default)
    {
        var menu = await GetMenuOrThrowAsync(id, cancellationToken);

        if (request.ParentId == id)
        {
            throw new BusinessException(ErrorCode.BadRequest, "Menu cannot be its own parent.");
        }

        if (request.ParentId.HasValue && await _menuRepository.GetByIdAsync(request.ParentId.Value, cancellationToken) is null)
        {
            throw new BusinessException(ErrorCode.BadRequest, "Parent menu is invalid.");
        }

        menu.ParentId = request.ParentId;
        menu.Name = request.Name.Trim();
        menu.Path = request.Path;
        menu.Component = request.Component;
        menu.Redirect = request.Redirect;
        menu.Icon = request.Icon;
        menu.Sort = request.Sort;
        menu.Visible = request.Visible;
        menu.KeepAlive = request.KeepAlive;
        menu.MenuType = request.MenuType;
        menu.PermissionCode = request.PermissionCode;

        _menuRepository.Update(menu);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(menu);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var menu = await GetMenuOrThrowAsync(id, cancellationToken);
        if (_menuRepository.Query().Any(entity => entity.ParentId == id))
        {
            throw new BusinessException(ErrorCode.Conflict, "Please delete child menus first.");
        }

        foreach (var relation in _roleMenuRepository.Query().Where(entity => entity.MenuId == id).ToList())
        {
            _roleMenuRepository.Remove(relation);
        }

        _menuRepository.Remove(menu);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public static IReadOnlyList<MenuTreeResponse> BuildTree(IReadOnlyCollection<Menu> menus)
    {
        return menus
            .Where(entity => entity.ParentId is null)
            .OrderBy(entity => entity.Sort)
            .Select(entity => BuildNode(entity, menus))
            .ToList();
    }

    public static MenuTreeResponse ToResponse(Menu menu)
    {
        return new MenuTreeResponse
        {
            Id = menu.Id,
            TenantId = menu.TenantId,
            ParentId = menu.ParentId,
            Name = menu.Name,
            Path = menu.Path,
            Component = menu.Component,
            Redirect = menu.Redirect,
            Icon = menu.Icon,
            Sort = menu.Sort,
            Visible = menu.Visible,
            KeepAlive = menu.KeepAlive,
            MenuType = menu.MenuType,
            PermissionCode = menu.PermissionCode
        };
    }

    private static MenuTreeResponse BuildNode(Menu menu, IReadOnlyCollection<Menu> menus)
    {
        var children = menus
            .Where(entity => entity.ParentId == menu.Id)
            .OrderBy(entity => entity.Sort)
            .Select(entity => BuildNode(entity, menus))
            .ToList();

        return new MenuTreeResponse
        {
            Id = menu.Id,
            TenantId = menu.TenantId,
            ParentId = menu.ParentId,
            Name = menu.Name,
            Path = menu.Path,
            Component = menu.Component,
            Redirect = menu.Redirect,
            Icon = menu.Icon,
            Sort = menu.Sort,
            Visible = menu.Visible,
            KeepAlive = menu.KeepAlive,
            MenuType = menu.MenuType,
            PermissionCode = menu.PermissionCode,
            Children = children
        };
    }

    private async Task<Menu> GetMenuOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _menuRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Menu was not found.");
    }

    private static void ValidateRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }
    }
}
