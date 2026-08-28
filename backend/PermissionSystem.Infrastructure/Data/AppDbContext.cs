using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly IAuditContext _auditContext;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantContext tenantContext,
        IAuditContext auditContext)
        : base(options)
    {
        _tenantContext = tenantContext;
        _auditContext = auditContext;
    }

    public Guid? CurrentTenantId => _tenantContext.TenantId;

    public bool IsSystemTenantScopeActive => _tenantContext.IsSystemScopeActive;

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Menu> Menus => Set<Menu>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RoleDataScope> RoleDataScopes => Set<RoleDataScope>();

    public DbSet<UserDataScope> UserDataScopes => Set<UserDataScope>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();

    public DbSet<DictionaryType> DictionaryTypes => Set<DictionaryType>();

    public DbSet<DictionaryItem> DictionaryItems => Set<DictionaryItem>();

    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    public DbSet<FileResource> FileResources => Set<FileResource>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<DeadLetterMessage> DeadLetterMessages => Set<DeadLetterMessage>();

    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();

    public DbSet<ScheduledTaskExecutionLog> ScheduledTaskExecutionLogs => Set<ScheduledTaskExecutionLog>();

    public DbSet<JobExecutionLog> JobExecutionLogs => Set<JobExecutionLog>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<NumberRule> NumberRules => Set<NumberRule>();

    public DbSet<NumberRuleSegment> NumberRuleSegments => Set<NumberRuleSegment>();

    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    public DbSet<StateMachineDefinition> StateMachineDefinitions => Set<StateMachineDefinition>();

    public DbSet<StateDefinition> StateDefinitions => Set<StateDefinition>();

    public DbSet<StateTransition> StateTransitions => Set<StateTransition>();

    public DbSet<StateTransitionLog> StateTransitionLogs => Set<StateTransitionLog>();

    public DbSet<PrintTemplate> PrintTemplates => Set<PrintTemplate>();

    public DbSet<PrintRecord> PrintRecords => Set<PrintRecord>();

    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();

    public DbSet<ReportQueryParam> ReportQueryParams => Set<ReportQueryParam>();

    public DbSet<ReportExecutionLog> ReportExecutionLogs => Set<ReportExecutionLog>();

    public DbSet<SecurityPolicy> SecurityPolicies => Set<SecurityPolicy>();

    public DbSet<LoginFailureRecord> LoginFailureRecords => Set<LoginFailureRecord>();

    public DbSet<SensitiveOperationVerification> SensitiveOperationVerifications => Set<SensitiveOperationVerification>();

    public DbSet<IpAccessRule> IpAccessRules => Set<IpAccessRule>();

    public DbSet<ApiClient> ApiClients => Set<ApiClient>();

    public DbSet<ApiClientSecret> ApiClientSecrets => Set<ApiClientSecret>();

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();

    public DbSet<ExternalApiCallLog> ExternalApiCallLogs => Set<ExternalApiCallLog>();

    public DbSet<SsoProvider> SsoProviders => Set<SsoProvider>();

    public DbSet<SsoUserBinding> SsoUserBindings => Set<SsoUserBinding>();

    public DbSet<SsoRoleMapping> SsoRoleMappings => Set<SsoRoleMapping>();

    public DbSet<SsoDepartmentMapping> SsoDepartmentMappings => Set<SsoDepartmentMapping>();

    public DbSet<SsoLoginLog> SsoLoginLogs => Set<SsoLoginLog>();

    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();

    public DbSet<WorkflowNode> WorkflowNodes => Set<WorkflowNode>();

    public DbSet<WorkflowEdge> WorkflowEdges => Set<WorkflowEdge>();

    public DbSet<WorkflowCondition> WorkflowConditions => Set<WorkflowCondition>();

    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();

    public DbSet<WorkflowRecord> WorkflowRecords => Set<WorkflowRecord>();

    public DbSet<WorkflowCc> WorkflowCcs => Set<WorkflowCc>();

    public DbSet<WorkflowBusinessBinding> WorkflowBusinessBindings => Set<WorkflowBusinessBinding>();

    public DbSet<DemoApprovalOrder> DemoApprovalOrders => Set<DemoApprovalOrder>();

    public DbSet<DemoBusinessOrder> DemoBusinessOrders => Set<DemoBusinessOrder>();

    public DbSet<AiProviderConfig> AiProviderConfigs => Set<AiProviderConfig>();

    public DbSet<AiConversation> AiConversations => Set<AiConversation>();

    public DbSet<AiMessage> AiMessages => Set<AiMessage>();

    public DbSet<AiRun> AiRuns => Set<AiRun>();

    public DbSet<AiToolInvocation> AiToolInvocations => Set<AiToolInvocation>();

    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    public DbSet<AiModelRoutePolicy> AiModelRoutePolicies => Set<AiModelRoutePolicy>();

    public DbSet<AiBudgetPolicy> AiBudgetPolicies => Set<AiBudgetPolicy>();

    public DbSet<AiUserFeedback> AiUserFeedbacks => Set<AiUserFeedback>();

    public DbSet<AiDocumentDraft> AiDocumentDrafts => Set<AiDocumentDraft>();

    public DbSet<AiDocumentDraftValidation> AiDocumentDraftValidations => Set<AiDocumentDraftValidation>();

    public DbSet<AiDocumentConfirmation> AiDocumentConfirmations => Set<AiDocumentConfirmation>();

    public DbSet<AiDocumentExecution> AiDocumentExecutions => Set<AiDocumentExecution>();

    public DbSet<McpClientBinding> McpClientBindings => Set<McpClientBinding>();

    public DbSet<McpDatasetDefinition> McpDatasetDefinitions => Set<McpDatasetDefinition>();

    public DbSet<McpDatasetField> McpDatasetFields => Set<McpDatasetField>();

    public DbSet<McpClientDatasetGrant> McpClientDatasetGrants => Set<McpClientDatasetGrant>();

    public DbSet<McpInvocationLog> McpInvocationLogs => Set<McpInvocationLog>();

    public override int SaveChanges()
    {
        ApplyAuditFields();
        try
        {
            return base.SaveChanges();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw CreateConcurrencyException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw CreateUniqueConstraintException(exception);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw CreateConcurrencyException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw CreateUniqueConstraintException(exception);
        }
    }

    private static BusinessException CreateConcurrencyException(Exception innerException)
    {
        return new BusinessException(
            ErrorCode.Conflict,
            "The resource was modified by another request.",
            innerException);
    }

    private static BusinessException CreateUniqueConstraintException(Exception innerException)
    {
        return new BusinessException(
            ErrorCode.Conflict,
            "A resource with the same unique value already exists.",
            innerException);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException &&
                sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.UseOpenIddict();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyBaseEntityQueryFilters(modelBuilder);
    }

    private void ApplyBaseEntityQueryFilters(ModelBuilder modelBuilder)
    {
        var entityTypes = modelBuilder.Model
            .GetEntityTypes()
            .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType));

        foreach (var entityType in entityTypes)
        {
            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeletedProperty = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var notDeleted = Expression.Equal(isDeletedProperty, Expression.Constant(false));

            var tenantIdProperty = Expression.Convert(
                Expression.Property(parameter, nameof(BaseEntity.TenantId)),
                typeof(Guid?));
            var currentTenantId = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
            var systemScopeActive = Expression.Property(Expression.Constant(this), nameof(IsSystemTenantScopeActive));
            var tenantMatched = Expression.Equal(tenantIdProperty, currentTenantId);
            var tenantFilter = Expression.OrElse(systemScopeActive, tenantMatched);
            var filterBody = Expression.AndAlso(notDeleted, tenantFilter);
            var filter = Expression.Lambda(filterBody, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    private void ApplyAuditFields()
    {
        var now = DateTimeOffset.UtcNow;
        var currentUserId = _auditContext.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.Id == Guid.Empty)
                {
                    entry.Entity.Id = Guid.NewGuid();
                }

                ApplyTenantId(entry.Entity);
            }

            ValidateTenantWrite(entry);

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= currentUserId;
                    entry.Entity.IsDeleted = false;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUserId ?? entry.Entity.UpdatedBy;
                    PreserveCreationAuditFields(entry);
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUserId ?? entry.Entity.UpdatedBy;
                    PreserveCreationAuditFields(entry);
                    break;
            }
        }
    }

    private static void PreserveCreationAuditFields(EntityEntry<BaseEntity> entry)
    {
        entry.Property(entity => entity.CreatedAt).IsModified = false;
        entry.Property(entity => entity.CreatedBy).IsModified = false;
    }

    private void ApplyTenantId(BaseEntity entity)
    {
        if (entity is Tenant && entity.TenantId == Guid.Empty)
        {
            entity.TenantId = entity.Id;
            return;
        }

        if (entity.TenantId == Guid.Empty && _tenantContext.TenantId.HasValue)
        {
            entity.TenantId = _tenantContext.TenantId.Value;
        }
    }

    private void ValidateTenantWrite(EntityEntry<BaseEntity> entry)
    {
        if (entry.State is EntityState.Detached or EntityState.Unchanged)
        {
            return;
        }

        var tenantIdProperty = entry.Property(entity => entity.TenantId);
        if (entry.State != EntityState.Added &&
            tenantIdProperty.OriginalValue != tenantIdProperty.CurrentValue)
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "TenantId cannot be changed after an entity is created.");
        }

        if (entry.Entity is Tenant)
        {
            if (entry.Entity.TenantId != entry.Entity.Id)
            {
                throw new BusinessException(
                    ErrorCode.ValidationFailed,
                    "A tenant entity must use its own Id as TenantId.");
            }

            if (!_tenantContext.IsSystemScopeActive && !_tenantContext.IsResolved)
            {
                throw new BusinessException(
                    ErrorCode.ValidationFailed,
                    "Tenant context or an explicit system tenant scope is required when writing tenant data.");
            }

            return;
        }

        if (_tenantContext.IsSystemScopeActive)
        {
            return;
        }

        if (!_tenantContext.IsResolved)
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "Tenant context is required when writing tenant data.");
        }

        if (_tenantContext.IsSuperAdmin &&
            !IsExplicitTenantSelection(_tenantContext.Source) &&
            !IsSessionHeartbeat(entry))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                "Super administrators must explicitly select a target tenant before writing tenant data.");
        }

        if (entry.Entity.TenantId != _tenantContext.TenantId)
        {
            throw new BusinessException(ErrorCode.Forbidden, "Cross-tenant writes are not allowed.");
        }
    }

    private static bool IsExplicitTenantSelection(string? source)
    {
        return string.Equals(source, "Header", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "Request", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSessionHeartbeat(EntityEntry<BaseEntity> entry)
    {
        if (entry.Entity is not UserSession || entry.State != EntityState.Modified)
        {
            return false;
        }

        return entry.Properties
            .Where(property => property.IsModified)
            .Select(property => property.Metadata.Name)
            .SequenceEqual([nameof(UserSession.LastActiveAt)]);
    }
}
