using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiToolServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ListDatasetsAsync_ReturnsOnlyP0PublicDataset_ForAuthorizedCaller()
    {
        var currentUser = new TestCurrentUserService(TenantId, [AiCenterConstants.McpDatasetQueryPermission]);
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId, "Claims");
        var service = new AiToolService(currentUser, tenantContext, new TraceContextAccessor { TraceId = "trace-1" });

        var result = await service.ListDatasetsAsync();

        var dataset = Assert.Single(result.Data);
        Assert.Equal("platform-capabilities", dataset.Key);
        Assert.Equal("Public", dataset.DataClassification);
        Assert.Equal("trace-1", result.TraceId);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task ListDatasetsAsync_RejectsCallerWithoutPermission()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId, "Claims");
        var service = new AiToolService(
            new TestCurrentUserService(TenantId, []),
            tenantContext,
            new TraceContextAccessor());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.ListDatasetsAsync());

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    [Fact]
    public async Task ListDatasetsAsync_RejectsMismatchedTenantContext()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid(), "Claims");
        var service = new AiToolService(
            new TestCurrentUserService(TenantId, [AiCenterConstants.McpDatasetQueryPermission]),
            tenantContext,
            new TraceContextAccessor());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.ListDatasetsAsync());

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid tenantId, IReadOnlyCollection<string> permissions)
        {
            TenantId = tenantId;
            PermissionCodes = permissions;
        }

        public bool IsAuthenticated => true;
        public Guid? UserId => Guid.Parse("30000000-0000-0000-0000-000000000001");
        public Guid? TenantId { get; }
        public Guid? DepartmentId => null;
        public string? SessionId => "session";
        public string? Username => "tester";
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> PermissionCodes { get; }
        public bool IsSuperAdmin => false;
        public bool IsCurrentUserSuperAdmin() => false;
        public bool IsCurrentUserAdmin() => false;
        public bool CanManageBuiltinResources() => false;
        public bool HasPermission(string permissionCode) =>
            PermissionCodes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }
}
