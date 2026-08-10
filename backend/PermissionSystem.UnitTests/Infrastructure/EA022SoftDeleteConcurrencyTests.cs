using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.UnitTests.Infrastructure;

public sealed class EA022SoftDeleteConcurrencyTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000022");

    [Fact]
    public void EveryBaseEntity_ShouldUseRowVersionConcurrencyToken()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes()
            .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            .ToArray();

        Assert.NotEmpty(entityTypes);
        foreach (var entityType in entityTypes)
        {
            var property = entityType.FindProperty(nameof(BaseEntity.RowVersion));

            Assert.NotNull(property);
            Assert.True(property!.IsConcurrencyToken, entityType.ClrType.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        }
    }

    [Fact]
    public void ReusableBusinessKeys_ShouldUseActiveRowUniqueIndexes()
    {
        using var context = CreateContext();

        AssertUniqueFilter<WorkflowNode>(context, "[IsDeleted] = 0", "TenantId", "DefinitionId", "NodeKey");
        AssertUniqueFilter<WorkflowDefinition>(context, "[IsDeleted] = 0", "TenantId", "Code", "Version");
        AssertUniqueFilter<WorkflowBusinessBinding>(context, "[IsDeleted] = 0", "TenantId", "BusinessType");
        AssertUniqueFilter<User>(context, "[IsDeleted] = 0", "TenantId", "NormalizedUserName");
        AssertUniqueFilter<UserRole>(context, "[IsDeleted] = 0", "TenantId", "UserId", "RoleId");
        AssertUniqueFilter<UserDataScope>(context, "[IsDeleted] = 0", "TenantId", "UserId");
        AssertUniqueFilter<SystemConfig>(context, "[IsDeleted] = 0", "TenantId", "ConfigKey");
        AssertUniqueFilter<StateMachineDefinition>(context, "[IsDeleted] = 0", "TenantId", "BusinessType");
        AssertUniqueFilter<StateDefinition>(context, "[IsDeleted] = 0", "TenantId", "MachineId", "StateCode");
        AssertUniqueFilter<SsoUserBinding>(context, "[IsDeleted] = 0", "TenantId", "ProviderId", "ExternalUserId");
        AssertUniqueFilter<SsoUserBinding>(context, "[IsDeleted] = 0", "TenantId", "ProviderId", "LocalUserId");
        AssertUniqueFilter<SsoRoleMapping>(context, "[IsDeleted] = 0", "TenantId", "ProviderId", "ExternalRole", "LocalRoleId");
        AssertUniqueFilter<SsoProvider>(context, "[IsDeleted] = 0", "TenantId", "ProviderCode");
        AssertUniqueFilter<SsoDepartmentMapping>(context, "[IsDeleted] = 0", "TenantId", "ProviderId", "ExternalDepartment", "LocalDepartmentId");
        AssertUniqueFilter<ScheduledTask>(context, "[IsDeleted] = 0", "TenantId", "Code");
        AssertUniqueFilter<Role>(context, "[IsDeleted] = 0", "TenantId", "Code");
        AssertUniqueFilter<RolePermission>(context, "[IsDeleted] = 0", "TenantId", "RoleId", "PermissionId");
        AssertUniqueFilter<RoleMenu>(context, "[IsDeleted] = 0", "TenantId", "RoleId", "MenuId");
        AssertUniqueFilter<RoleDataScope>(context, "[IsDeleted] = 0", "TenantId", "RoleId");
        AssertUniqueFilter<ReportQueryParam>(context, "[IsDeleted] = 0", "TenantId", "ReportId", "ParamCode");
        AssertUniqueFilter<ReportDefinition>(context, "[IsDeleted] = 0", "TenantId", "ReportCode");
        AssertUniqueFilter<PrintTemplate>(context, "[IsDeleted] = 0", "TenantId", "TemplateCode");
        AssertUniqueFilter<Permission>(context, "[IsDeleted] = 0", "TenantId", "Code");
        AssertUniqueFilter<NumberRule>(context, "[IsDeleted] = 0", "TenantId", "RuleCode");
        AssertUniqueFilter<NotificationTemplate>(context, "[IsDeleted] = 0", "TenantId", "Code");
        AssertUniqueFilter<LoginFailureRecord>(context, "[IsDeleted] = 0 AND [IpAddress] IS NOT NULL", "TenantId", "UserName", "IpAddress");
        AssertUniqueFilter<IpAccessRule>(context, "[IsDeleted] = 0", "TenantId", "RuleType", "IpPattern");
        AssertUniqueFilter<DictionaryType>(context, "[IsDeleted] = 0", "TenantId", "Code");
        AssertUniqueFilter<DictionaryItem>(context, "[IsDeleted] = 0", "TenantId", "TypeCode", "Value");
        AssertUniqueFilter<Department>(context, "[IsDeleted] = 0", "TenantId", "Code");
        AssertUniqueFilter<DemoBusinessOrder>(context, "[IsDeleted] = 0", "TenantId", "OrderNo");
        AssertUniqueFilter<DemoApprovalOrder>(context, "[IsDeleted] = 0", "TenantId", "OrderNo");
        AssertUniqueFilter<ApiClient>(context, "[IsDeleted] = 0", "TenantId", "ClientCode");
    }

    [Fact]
    public void PermanentUniqueKeys_ShouldRemainUnfiltered()
    {
        using var context = CreateContext();

        AssertUniqueFilter<Tenant>(context, null, "Code");
        AssertUniqueFilter<ApiClientSecret>(context, null, "TenantId", "SecretHash");
        AssertUniqueFilter<InboxMessage>(context, null, "TenantId", "MessageId", "Consumer");
        AssertUniqueFilter<OutboxMessage>(context, null, "TenantId", "MessageId");
        AssertUniqueFilter<NumberSequence>(context, null, "TenantId", "RuleCode", "SequenceKey");
        AssertUniqueFilter<SecurityPolicy>(context, null, "TenantId");
        AssertUniqueFilter<UserNotification>(context, null, "TenantId", "UserId", "NotificationId");
        AssertUniqueFilter<UserSession>(context, null, "SessionId");
    }

    [Fact]
    public void ConcurrencyTokenGuard_ShouldAllowMissingOrMatchingToken_AndRejectMismatch()
    {
        var entity = new Department { RowVersion = [1, 2, 3, 4] };

        ConcurrencyTokenGuard.EnsureMatches(entity, null);
        ConcurrencyTokenGuard.EnsureMatches(entity, []);
        ConcurrencyTokenGuard.EnsureMatches(entity, [1, 2, 3, 4]);

        var exception = Assert.Throws<BusinessException>(() =>
            ConcurrencyTokenGuard.EnsureMatches(entity, [4, 3, 2, 1]));

        Assert.Equal(ErrorCode.Conflict, exception.ErrorCode);
    }

    private static void AssertUniqueFilter<TEntity>(
        AppDbContext context,
        string? expectedFilter,
        params string[] propertyNames)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);
        var index = entityType!.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.True(index.IsUnique, $"{typeof(TEntity).Name}: {string.Join(", ", propertyNames)}");
        Assert.Equal(expectedFilter, index.GetFilter());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options, new TestTenantContext(TenantId), new NullAuditContext());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
        }

        public Guid? TenantId { get; }

        public string? Source => "Test";

        public bool IsResolved => true;

        public bool IsSuperAdmin => false;

        public bool IsSystemScopeActive => false;

        public bool IsHttpRequest => false;

        public void SetTenant(Guid tenantId, string source)
        {
        }

        public void MarkAsSuperAdmin(bool isSuperAdmin)
        {
        }

        public void MarkAsHttpRequest()
        {
        }
    }
}
