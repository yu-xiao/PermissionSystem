using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Authentication;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.AiKnowledge;

public sealed class AiKnowledgeAccessPolicy(
    ICurrentUserService current, ITenantContext tenant, IUserCredentialValidator identities,
    IAiCenterConfiguration configuration, IAiToolConfiguration tools, IAsyncQueryExecutor queries,
    IRepository<AiKnowledgeDocument> documents, IRepository<AiKnowledgeDocumentVersion> versions,
    IRepository<AiKnowledgeDocumentRole> grants, IRepository<Role> roles, IRepository<UserRole> memberships,
    IRepository<FileResource> files)
{
    public async Task<AuthenticatedUser> AuthorizeAsync(string permission, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not Guid userId || current.TenantId is not Guid tenantId)
            throw new BusinessException(ErrorCode.Unauthorized, "A valid knowledge identity is required.");
        if (tenant.TenantId != tenantId || !configuration.Enabled || !tools.EnableKnowledgeDocumentTool ||
            !configuration.AllowedTenantIds.Contains(tenantId))
            throw new BusinessException(ErrorCode.Forbidden, "Knowledge documents are disabled.");
        var actor = await identities.GetAuthenticationStateAsync(tenantId, userId, ct);
        if (actor is null || actor.UserId != userId || actor.TenantId != tenantId ||
            actor.DepartmentId != current.DepartmentId || PermissionEvaluation.IsSuperAdmin(actor.Roles) != current.IsSuperAdmin)
            throw new BusinessException(ErrorCode.Unauthorized, "The knowledge identity is inactive or stale.");
        if (!current.HasPermission(permission) || !PermissionEvaluation.HasPermission(true,
            PermissionEvaluation.IsSuperAdmin(actor.Roles), actor.PermissionCodes, permission))
            throw new BusinessException(ErrorCode.Forbidden, "Knowledge access is not authorized.");
        return actor;
    }

    public IQueryable<AiKnowledgeDocument> GrantedDocuments(AuthenticatedUser actor)
    {
        var grantQuery = grants.Query();
        var roleQuery = roles.Query();
        var memberQuery = memberships.Query();
        return documents.Query().Where(d => !d.IsDeleted && d.TenantId == actor.TenantId &&
            grantQuery.Any(g => !g.IsDeleted && g.TenantId == actor.TenantId && g.DocumentId == d.Id &&
                roleQuery.Any(r => !r.IsDeleted && r.IsEnabled && r.TenantId == actor.TenantId && r.Id == g.RoleId &&
                    memberQuery.Any(m => !m.IsDeleted && m.TenantId == actor.TenantId &&
                        m.UserId == actor.UserId && m.RoleId == r.Id))));
    }

    public async Task<bool> CanReadDocumentAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var actor = await AuthorizeAsync(AiCenterConstants.KnowledgeQueryPermission, ct);
            return await queries.AnyAsync(VisibleVersions(actor, DateTimeOffset.UtcNow).Where(v => v.DocumentId == id), ct);
        }
        catch (BusinessException e) when (e.ErrorCode is ErrorCode.Forbidden or ErrorCode.Unauthorized) { return false; }
    }

    public IQueryable<AiKnowledgeDocumentVersion> ReviewableVersions(AuthenticatedUser actor, DateTimeOffset now) =>
        from version in versions.Query()
        join document in GrantedDocuments(actor) on version.DocumentId equals document.Id
        join file in files.Query() on version.FileResourceId equals file.Id
        where !version.IsDeleted && version.TenantId == actor.TenantId &&
            version.ParseStatus == AiKnowledgeParseStatus.Ready && version.ValidUntil > now && !file.IsDeleted && file.TenantId == actor.TenantId &&
            file.BusinessType == AiKnowledgeContract.BusinessType && file.BusinessId == document.Id &&
            file.FileStatus == FileStatus.Active && file.ScanStatus == FileScanStatus.Clean
        select version;

    public IQueryable<AiKnowledgeDocumentVersion> VisibleVersions(AuthenticatedUser actor, DateTimeOffset now)
    {
        var documentQuery = documents.Query();
        return ReviewableVersions(actor, now).Where(version => version.PublishedAt != null && version.ValidFrom <= now &&
            documentQuery.Any(d => !d.IsDeleted && d.TenantId == actor.TenantId && d.Id == version.DocumentId && d.CurrentVersionId == version.Id));
    }

    public async Task<bool> CanReadFileAsync(FileResource file, CancellationToken ct)
    {
        try
        {
            var actor = await AuthorizeAsync(AiCenterConstants.KnowledgeQueryPermission, ct);
            return file.TenantId == actor.TenantId && await queries.AnyAsync(
                VisibleVersions(actor, DateTimeOffset.UtcNow).Where(v => v.FileResourceId == file.Id), ct);
        }
        catch (BusinessException e) when (e.ErrorCode is ErrorCode.Forbidden or ErrorCode.Unauthorized) { return false; }
    }

    public async Task<bool> CanManageAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var actor = await AuthorizeAsync(AiCenterConstants.KnowledgeManagePermission, ct);
            return await queries.AnyAsync(documents.Query().Where(d => d.TenantId == actor.TenantId && d.Id == id && !d.IsDeleted), ct);
        }
        catch (BusinessException e) when (e.ErrorCode is ErrorCode.Forbidden or ErrorCode.Unauthorized) { return false; }
    }
}
