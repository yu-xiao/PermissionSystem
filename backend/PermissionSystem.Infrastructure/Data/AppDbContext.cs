using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;

namespace PermissionSystem.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public Guid? CurrentTenantId => _tenantContext.TenantId;

    public bool TenantFilterDisabled => _tenantContext.IsTenantFilterDisabled || !_tenantContext.TenantId.HasValue;

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

    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();

    public DbSet<ScheduledTaskExecutionLog> ScheduledTaskExecutionLogs => Set<ScheduledTaskExecutionLog>();

    public DbSet<JobExecutionLog> JobExecutionLogs => Set<JobExecutionLog>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

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

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(cancellationToken);
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
            var tenantFilterDisabled = Expression.Property(Expression.Constant(this), nameof(TenantFilterDisabled));
            var tenantMatched = Expression.Equal(tenantIdProperty, currentTenantId);
            var tenantFilter = Expression.OrElse(tenantFilterDisabled, tenantMatched);
            var filterBody = Expression.AndAlso(notDeleted, tenantFilter);
            var filter = Expression.Lambda(filterBody, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    private void ApplyAuditFields()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.Id == Guid.Empty)
                    {
                        entry.Entity.Id = Guid.NewGuid();
                    }

                    ApplyTenantId(entry.Entity);
                    entry.Entity.CreatedAt = now;
                    entry.Entity.IsDeleted = false;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Property(entity => entity.CreatedAt).IsModified = false;
                    entry.Property(entity => entity.CreatedBy).IsModified = false;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
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
}
