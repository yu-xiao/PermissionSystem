using PermissionSystem.Application.DemoBusinessOrders;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.DemoBusinessOrders;

public sealed class DemoBusinessOrderValidatorTests
{
    [Fact]
    public async Task EnsureDepartmentAvailable_AllowsEnabledDepartmentInCurrentTenant()
    {
        var department = CreateDepartment(TestIds.TenantId, isEnabled: true);
        var validator = CreateValidator(department);

        await validator.EnsureDepartmentAvailableAsync(department.Id, TestIds.TenantId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task EnsureDepartmentAvailable_RejectsUnavailableDepartment(
        bool crossTenant,
        bool disabled)
    {
        var department = CreateDepartment(
            crossTenant ? Guid.NewGuid() : TestIds.TenantId,
            !disabled);
        var validator = CreateValidator(department);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            validator.EnsureDepartmentAvailableAsync(
                crossTenant || disabled ? department.Id : Guid.NewGuid(),
                TestIds.TenantId));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    private static DemoBusinessOrderValidator CreateValidator(params Department[] departments) =>
        new(new InMemoryRepository<Department>(departments), new InMemoryAsyncQueryExecutor());

    private static Department CreateDepartment(Guid tenantId, bool isEnabled) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Code = "SALES",
        Name = "Sales",
        IsEnabled = isEnabled
    };
}
