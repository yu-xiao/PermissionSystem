using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Application.Menus;
using PermissionSystem.Application.Users;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Permissions;

public sealed class PermissionDiagnosticService : IPermissionDiagnosticService
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantContext _tenant;
    private readonly IUserCredentialValidator _identities;
    private readonly IRepository<User> _users;
    private readonly IRepository<Menu> _menus;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<UserRole> _userRoles;
    private readonly IRepository<RolePermission> _rolePermissions;
    private readonly IRepository<RoleMenu> _roleMenus;
    private readonly IRepository<Domain.Entities.Permission> _permissions;
    private readonly IDataScopeResolver _scopes;
    private readonly IDataPermissionFilter _filter;
    private readonly IUserMenuResolver _menuResolver;
    private readonly IAsyncQueryExecutor _queries;
    private readonly IAiCenterConfiguration _configuration;

    public PermissionDiagnosticService(
        ICurrentUserService currentUser, ITenantContext tenant, IUserCredentialValidator identities,
        IRepository<User> users, IRepository<Menu> menus, IRepository<Role> roles,
        IRepository<UserRole> userRoles, IRepository<RolePermission> rolePermissions, IRepository<RoleMenu> roleMenus,
        IRepository<Domain.Entities.Permission> permissions,
        IDataScopeResolver scopes, IDataPermissionFilter filter, IUserMenuResolver menuResolver,
        IAsyncQueryExecutor queries, IAiCenterConfiguration configuration)
    {
        _currentUser = currentUser;
        _tenant = tenant;
        _identities = identities;
        _users = users;
        _menus = menus;
        _roles = roles;
        _userRoles = userRoles;
        _rolePermissions = rolePermissions;
        _roleMenus = roleMenus;
        _permissions = permissions;
        _scopes = scopes;
        _filter = filter;
        _menuResolver = menuResolver;
        _queries = queries;
        _configuration = configuration;
    }

    public async Task<PermissionDiagnosticResponse> DiagnoseAsync(
        PermissionDiagnosticRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var access = await AuthorizeAsync(request, cancellationToken);
        var isSelf = access.Target.Id == _currentUser.UserId;
        var subject = new PermissionSubject(access.Target.TenantId, access.Target.Id,
            isSelf ? _currentUser.DepartmentId : access.Target.DepartmentId,
            isSelf ? _currentUser.IsSuperAdmin : PermissionEvaluation.IsSuperAdmin(access.State?.Roles ?? []));
        var checks = new List<PermissionDiagnosticCheck>
        {
            new("identity.active", access.Target.IsEnabled ? PermissionDiagnosticCheckStatus.Passed : PermissionDiagnosticCheckStatus.Failed,
                access.Target.IsEnabled ? "身份启用且租户活动。" : "目标用户已停用，角色配置不能使其获得访问权限。", "AuthenticationState")
        };
        var limitations = new List<string>
        {
            "未检查目标浏览器缓存、会话撤销/过期及客户端实际运行状态。",
            "未执行真实业务请求，未检查资源归属、其他授权策略或业务状态校验。"
        };
        if (!isSelf)
        {
            limitations.Add("这是当前配置推算，不代表目标用户现有 Token 的授权状态。");
        }

        PermissionDiagnosticConclusion conclusion;
        string summary;
        if (!access.Target.IsEnabled)
        {
            conclusion = PermissionDiagnosticConclusion.Denied;
            summary = "目标身份停用，不能据现有角色配置认定可访问。";
            checks.Add(new("resource.evaluation", PermissionDiagnosticCheckStatus.NotEvaluated,
                "身份停用，未继续计算资源授权。", "AuthenticationState"));
        }
        else if (request.Kind == PermissionDiagnosticKind.Permission)
        {
            var codes = PermissionEvaluation.ParseRequirement(request.PermissionCode!);
            var allowed = codes.Any(code => isSelf ? _currentUser.HasPermission(code) :
                PermissionEvaluation.HasPermission(true, subject.IsSuperAdmin, access.State!.PermissionCodes, code));
            checks.Add(new("api.permission", allowed ? PermissionDiagnosticCheckStatus.Passed : PermissionDiagnosticCheckStatus.Failed,
                allowed ? "指定权限要求至少一项满足（含超级管理员及通配符规则）。" : "指定权限要求没有任何一项满足。", "PermissionAuthorizationHandler"));
            var frontend = subject.IsSuperAdmin || (isSelf ? _currentUser.PermissionCodes : access.State!.PermissionCodes)
                .Contains(request.PermissionCode!.Trim(), StringComparer.Ordinal);
            checks.Add(new("frontend.permission", frontend ? PermissionDiagnosticCheckStatus.Passed : PermissionDiagnosticCheckStatus.Failed,
                "前端普通用户按完整权限码精确匹配，不展开通配符或组合要求。", "auth/permission store"));
            conclusion = allowed ? PermissionDiagnosticConclusion.Allowed : PermissionDiagnosticConclusion.Denied;
            summary = allowed ? "指定 API 权限要求满足；不代表完整业务操作一定成功。" : "指定 API 权限要求不满足。";
            if (HasAccess(access.Actor, "system:role:view"))
            {
                await AddRoleEvidenceAsync(access.Target, codes, checks, cancellationToken);
                if (isSelf) limitations.Add("角色来源反映当前配置；API 权限结论使用当前服务端身份声明，两者可能处于不同评估时点。");
            }
            else limitations.Add("没有角色查看权限，未披露角色配置来源。");
        }
        else if (request.Kind == PermissionDiagnosticKind.Menu)
        {
            var tree = await _menuResolver.ResolveMenusAsync(subject, cancellationToken);
            var visible = FindMenu(tree, request.MenuId!.Value);
            checks.Add(new("menu.tree", visible is null ? PermissionDiagnosticCheckStatus.Failed : PermissionDiagnosticCheckStatus.Passed,
                visible is null ? "目标不在当前计算得到的可见菜单树中。" : "目标在可见菜单树中（包含角色分配及祖先展开）。", "CurrentUserAppService"));
            conclusion = visible is null ? PermissionDiagnosticConclusion.InsufficientEvidence : PermissionDiagnosticConclusion.Allowed;
            summary = visible is null ? "目标不在可见菜单树中，尚不能确定完整原因。" : "菜单树包含目标；页面可访问性仍需检查路由和权限。";
            if (HasAccess(access.Actor, "system:menu:view"))
            {
                var configured = await _queries.FirstOrDefaultAsync(_menus.Query().Where(menu =>
                    menu.Id == request.MenuId && menu.TenantId == access.Target.TenantId), cancellationToken);
                checks.Add(new("menu.configuration", configured is null || !configured.Visible
                    ? PermissionDiagnosticCheckStatus.Failed : PermissionDiagnosticCheckStatus.Passed,
                    configured is null ? "当前租户没有可读取的目标菜单配置。" : configured.Visible
                        ? "目标菜单配置为可见；祖先及角色关系仍影响最终菜单树。" : "目标菜单配置为隐藏。", "MenuConfiguration"));
                if (configured is not null && HasAccess(access.Actor, "system:role:view"))
                {
                    var assigned = await _queries.AnyAsync(
                        from assignment in _userRoles.Query()
                        join role in _roles.Query() on assignment.RoleId equals role.Id
                        join relation in _roleMenus.Query() on role.Id equals relation.RoleId
                        where assignment.TenantId == access.Target.TenantId && assignment.UserId == access.Target.Id &&
                            role.TenantId == access.Target.TenantId && role.IsEnabled &&
                            relation.TenantId == access.Target.TenantId && relation.MenuId == request.MenuId
                        select relation, cancellationToken);
                    checks.Add(new("menu.role-assignment", assigned ? PermissionDiagnosticCheckStatus.Passed : PermissionDiagnosticCheckStatus.NotEvaluated,
                        assigned ? "启用角色直接分配了目标菜单；仍需满足可见性和祖先链条件。" : "没有启用角色直接分配目标；超级管理员及子菜单祖先展开仍可能使其进入菜单树。", "RoleMenu"));
                }
                if (visible is null) { conclusion = PermissionDiagnosticConclusion.Denied; summary = "目标未进入有效可见菜单树。"; }
                if (configured?.ParentId is not null)
                    await AddAncestorEvidenceAsync(configured, checks, cancellationToken);
            }
            else limitations.Add("没有菜单配置查看权限，不能区分隐藏、缺少配置或未分配等原因。");
            if (visible is not null)
            {
                var frontend = string.IsNullOrEmpty(visible.PermissionCode) || subject.IsSuperAdmin ||
                    (isSelf ? _currentUser.PermissionCodes : access.State!.PermissionCodes)
                        .Contains(visible.PermissionCode, StringComparer.Ordinal);
                checks.Add(new("frontend.route-permission", frontend ? PermissionDiagnosticCheckStatus.Passed : PermissionDiagnosticCheckStatus.Failed,
                    "按菜单关联权限码评估现有前端精确匹配；未验证组件注册及页面实际加载。", "auth/permission store"));
                if (!frontend) { conclusion = PermissionDiagnosticConclusion.Limited; summary = "菜单树包含目标，但前端关联权限码不满足。"; }
            }
            limitations.Add("菜单树存在并不证明前端组件、动态路由或按钮实际可用。");
        }
        else
        {
            var scope = await _scopes.ResolveAsync(subject, cancellationToken);
            var empty = !scope.HasAllDataScope && !scope.IncludesCurrentUser && scope.DepartmentIds.Count == 0;
            checks.Add(new("data-scope.rule", empty ? PermissionDiagnosticCheckStatus.Failed : PermissionDiagnosticCheckStatus.Passed,
                $"有效范围：{scope.ScopeType}；包含本人：{scope.IncludesCurrentUser}；部门数量：{scope.DepartmentIds.Count}。", "DataScopeService"));
            checks.Add(new("data-scope.source", PermissionDiagnosticCheckStatus.Passed,
                $"计算来源：{scope.ResolutionSource}；用户覆盖优先于启用角色的范围并集，无配置时默认本人。", "DataScopeService"));
            checks.Add(new("data-scope.resource", PermissionDiagnosticCheckStatus.NotEvaluated,
                "没有真实业务资源上下文，未判断某条数据是否可见。", "DataPermissionFilter"));
            conclusion = scope.HasAllDataScope ? PermissionDiagnosticConclusion.Allowed : empty
                ? PermissionDiagnosticConclusion.Denied : PermissionDiagnosticConclusion.Limited;
            summary = scope.HasAllDataScope ? "已知数据范围为全部；租户和其他业务约束仍适用。" : empty
                ? "已知数据范围不包含本人或任何部门。" : "已知数据范围受本人或部门规则限制。";
            limitations.Add("仅解释已知范围规则，不证明某条业务数据不可见的完整原因；未枚举部门和业务数据。");
        }

        var limitedChecks = checks.Take(PermissionDiagnosticResponse.MaxChecks).ToList();
        var truncated = checks.Count > limitedChecks.Count;
        var evaluatedAt = DateTimeOffset.UtcNow;
        var entries = GetEntries(access.Actor, request.Kind!.Value);
        PermissionDiagnosticResponse BuildResponse() => new()
        {
            Target = new PermissionDiagnosticTarget
            {
                UserId = access.Target.Id, Kind = request.Kind.Value,
                MenuId = request.MenuId, PermissionCode = request.PermissionCode?.Trim()
            },
            EvaluationBasis = isSelf ? "CurrentServerIdentity" : "CurrentConfiguration",
            EvaluatedAt = evaluatedAt,
            Conclusion = conclusion,
            Summary = summary,
            Checks = limitedChecks.ToList(),
            IsTruncated = truncated,
            Limitations = limitations,
            SuggestedEntries = entries
        };
        var result = BuildResponse();
        while (JsonSerializer.Serialize(result).Length > PermissionDiagnosticResponse.MaxSerializedLength)
        {
            var optionalIndex = limitedChecks.FindLastIndex(check => check.Source == "RolePermission");
            if (optionalIndex < 0)
                throw new BusinessException(ErrorCode.ValidationFailed, "Permission diagnostic evidence is too large.");
            limitedChecks.RemoveAt(optionalIndex);
            truncated = true;
            result = BuildResponse();
        }
        return result;
    }

    public async Task<bool> CanReadAsync(PermissionDiagnosticResponse result, CancellationToken cancellationToken = default)
    {
        if (result.Version != PermissionDiagnosticResponse.CurrentVersion || result.Target.UserId == Guid.Empty ||
            result.Checks.Count > PermissionDiagnosticResponse.MaxChecks) return false;
        try
        {
            var request = new PermissionDiagnosticRequest { Kind = result.Target.Kind, TargetUserId = result.Target.UserId,
                MenuId = result.Target.MenuId, PermissionCode = result.Target.PermissionCode };
            Validate(request);
            var access = await AuthorizeAsync(request, cancellationToken);
            return result.Checks.All(check => check.Source switch
            {
                "RolePermission" => HasAccess(access.Actor, "system:role:view"),
                "MenuConfiguration" => HasAccess(access.Actor, "system:menu:view"),
                "RoleMenu" => HasAccess(access.Actor, "system:menu:view") && HasAccess(access.Actor, "system:role:view"),
                _ => true
            });
        }
        catch (BusinessException exception) when (exception.ErrorCode is ErrorCode.Forbidden or
            ErrorCode.Unauthorized or ErrorCode.NotFound or ErrorCode.ValidationFailed)
        {
            return false;
        }
    }

    private async Task<(User Target, AuthenticatedUser Actor, AuthenticatedUser? State)> AuthorizeAsync(
        PermissionDiagnosticRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue || !_currentUser.TenantId.HasValue)
            throw new BusinessException(ErrorCode.Unauthorized, "Authentication is required.");
        var tenantId = _currentUser.TenantId.Value;
        if (tenantId == Guid.Empty || _tenant.TenantId != tenantId || !_configuration.Enabled ||
            !_configuration.AllowedTenantIds.Contains(tenantId))
            throw new BusinessException(ErrorCode.Forbidden, "Permission diagnostics are not available.");
        var actor = await _identities.GetAuthenticationStateAsync(tenantId, _currentUser.UserId.Value, cancellationToken)
            ?? throw new BusinessException(ErrorCode.Unauthorized, "The current identity is inactive.");
        if (actor.TenantId != tenantId || actor.UserId != _currentUser.UserId || !HasAccess(actor, AiCenterConstants.ToolQueryPermission))
            throw new BusinessException(ErrorCode.Forbidden, "Permission diagnostics are not available.");
        if (_currentUser.IsSuperAdmin != PermissionEvaluation.IsSuperAdmin(actor.Roles) ||
            _currentUser.DepartmentId != actor.DepartmentId)
            throw new BusinessException(ErrorCode.Unauthorized, "The current authorization identity is stale.");

        var targetId = request.TargetUserId ?? actor.UserId;
        var query = _users.Query().Where(user => user.TenantId == tenantId && user.Id == targetId);
        if (targetId != actor.UserId)
        {
            string[] required = [AiCenterConstants.UserQueryPermission, "system:user:view", "system:role:view",
                request.Kind switch { PermissionDiagnosticKind.Menu => "system:menu:view",
                    PermissionDiagnosticKind.Permission => "system:permission:view", _ => "system:role:data-scope" }];
            if (!required.All(permission => HasAccess(actor, permission)))
                throw UnavailableTarget();
            var scope = await _scopes.ResolveAsync(new PermissionSubject(tenantId, actor.UserId,
                actor.DepartmentId, PermissionEvaluation.IsSuperAdmin(actor.Roles)), cancellationToken);
            query = query.ApplyDataPermission(_filter, scope, user => (Guid?)user.Id, user => user.DepartmentId);
        }
        var target = await _queries.FirstOrDefaultAsync(query, cancellationToken) ?? throw UnavailableTarget();
        var state = targetId == actor.UserId ? actor : target.IsEnabled
            ? await _identities.GetAuthenticationStateAsync(tenantId, targetId, cancellationToken) : null;
        if (target.IsEnabled && (state is null || state.TenantId != tenantId || state.UserId != targetId))
            throw UnavailableTarget();
        return (target, actor, state);
    }

    private bool HasAccess(AuthenticatedUser actor, string code) =>
        _currentUser.HasPermission(code) && PermissionEvaluation.HasPermission(true,
            PermissionEvaluation.IsSuperAdmin(actor.Roles), actor.PermissionCodes, code);

    private static BusinessException UnavailableTarget() =>
        new(ErrorCode.Forbidden, "The diagnostic target is not available.");

    private async Task AddRoleEvidenceAsync(User target, IReadOnlyCollection<string> codes,
        List<PermissionDiagnosticCheck> checks, CancellationToken cancellationToken)
    {
        var normalized = codes.Select(code => code.ToUpperInvariant()).Append("*").ToArray();
        var query = from assignment in _userRoles.Query()
            join role in _roles.Query() on assignment.RoleId equals role.Id
            join relation in _rolePermissions.Query() on role.Id equals relation.RoleId
            join permission in _permissions.Query() on relation.PermissionId equals permission.Id
            where assignment.TenantId == target.TenantId && assignment.UserId == target.Id &&
                role.TenantId == target.TenantId && role.IsEnabled && relation.TenantId == target.TenantId &&
                permission.TenantId == target.TenantId && normalized.Contains(permission.Code.ToUpper())
            orderby role.Code, permission.Code
            select new { RoleCode = role.Code, PermissionCode = permission.Code };
        var evidence = await _queries.ToListAsync(query.Distinct().OrderBy(item => item.RoleCode)
            .ThenBy(item => item.PermissionCode).Take(PermissionDiagnosticResponse.MaxChecks + 1), cancellationToken);
        foreach (var item in evidence)
            checks.Add(new("roles.permission-source", PermissionDiagnosticCheckStatus.Passed,
                $"启用角色 {Clip(item.RoleCode)} 配置了 {Clip(item.PermissionCode)}。", "RolePermission"));
        if (evidence.Count == 0)
            checks.Add(new("roles.permission-source", PermissionDiagnosticCheckStatus.NotEvaluated,
                "没有找到请求权限的直接角色配置来源；超级管理员特例和身份声明仍按既有规则评估。", "RolePermission"));
    }

    private async Task AddAncestorEvidenceAsync(Menu menu, List<PermissionDiagnosticCheck> checks,
        CancellationToken cancellationToken)
    {
        var parentId = menu.ParentId;
        var visited = new HashSet<Guid> { menu.Id };
        while (parentId.HasValue)
        {
            if (!visited.Add(parentId.Value) || visited.Count > PermissionDiagnosticResponse.MaxChecks)
            {
                checks.Add(new("menu.ancestors", PermissionDiagnosticCheckStatus.NotEvaluated,
                    "祖先链循环或超过诊断深度上限，不能完整解释菜单树。", "MenuConfiguration"));
                return;
            }
            var parent = await _queries.FirstOrDefaultAsync(_menus.Query().Where(item =>
                item.Id == parentId.Value && item.TenantId == menu.TenantId), cancellationToken);
            if (parent is null || !parent.Visible)
            {
                checks.Add(new("menu.ancestors", PermissionDiagnosticCheckStatus.Failed,
                    parent is null ? "祖先菜单缺少可读取配置，目标不能连接到完整可见树。" : "祖先菜单隐藏，目标不能连接到完整可见树。", "MenuConfiguration"));
                return;
            }
            parentId = parent.ParentId;
        }
        checks.Add(new("menu.ancestors", PermissionDiagnosticCheckStatus.Passed,
            "已检查祖先链，所有祖先菜单均为可见。", "MenuConfiguration"));
    }

    private IReadOnlyList<PermissionDiagnosticEntry> GetEntries(AuthenticatedUser actor, PermissionDiagnosticKind kind)
    {
        var entries = new List<PermissionDiagnosticEntry>();
        if (HasAccess(actor, "system:user:view")) entries.Add(new("users", "用户配置"));
        if (HasAccess(actor, "system:role:view")) entries.Add(new("roles", "角色配置"));
        if (kind == PermissionDiagnosticKind.Menu && HasAccess(actor, "system:menu:view")) entries.Add(new("menus", "菜单配置"));
        if (kind == PermissionDiagnosticKind.Permission && HasAccess(actor, "system:permission:view")) entries.Add(new("permissions", "权限配置"));
        return entries;
    }

    private static string Clip(string value) => value.Length <= 100 ? value : value[..100];

    private static MenuTreeResponse? FindMenu(IEnumerable<MenuTreeResponse> tree, Guid id)
    {
        foreach (var item in tree)
        {
            if (item.Id == id) return item;
            var child = FindMenu(item.Children, id);
            if (child is not null) return child;
        }
        return null;
    }

    private static void Validate(PermissionDiagnosticRequest request)
    {
        if (!request.Kind.HasValue || !Enum.IsDefined(request.Kind.Value) || request.TargetUserId == Guid.Empty ||
            request.Kind == PermissionDiagnosticKind.Menu && (!request.MenuId.HasValue || request.MenuId == Guid.Empty || request.PermissionCode is not null) ||
            request.Kind != PermissionDiagnosticKind.Menu && request.MenuId.HasValue ||
            request.Kind == PermissionDiagnosticKind.DataScope && request.PermissionCode is not null)
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid permission diagnostic target.");
        if (request.Kind == PermissionDiagnosticKind.Permission &&
            (string.IsNullOrWhiteSpace(request.PermissionCode) || request.PermissionCode.Length > 500 ||
             PermissionEvaluation.ParseRequirement(request.PermissionCode).Count is 0 or > 10 ||
             PermissionEvaluation.ParseRequirement(request.PermissionCode).Any(code => code.Length > 100)))
            throw new BusinessException(ErrorCode.ValidationFailed, "Invalid permission requirement.");
    }
}
