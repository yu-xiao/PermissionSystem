using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Infrastructure.Data;

namespace PermissionSystem.UnitTests.Infrastructure;

public sealed class EA017ConcurrencyModelTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public void WorkflowAndStateEntities_ShouldUseRowVersionConcurrencyTokens()
    {
        using var context = CreateContext();

        foreach (var entityType in new[]
        {
            typeof(WorkflowTask),
            typeof(WorkflowInstance),
            typeof(DemoApprovalOrder),
            typeof(DemoBusinessOrder)
        })
        {
            var property = context.Model.FindEntityType(entityType)!
                .FindProperty(nameof(WorkflowTask.RowVersion));

            Assert.NotNull(property);
            Assert.True(property!.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        }
    }

    [Fact]
    public void RunningWorkflowInstance_ShouldHaveFilteredUniqueBusinessIndex()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(WorkflowInstance))!;
        var index = entityType.GetIndexes().Single(item =>
            item.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(WorkflowInstance.TenantId), nameof(WorkflowInstance.BusinessType), nameof(WorkflowInstance.BusinessId)]));

        Assert.True(index.IsUnique);
        Assert.Equal("[Status] = 0 AND [IsDeleted] = 0", index.GetFilter());
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
