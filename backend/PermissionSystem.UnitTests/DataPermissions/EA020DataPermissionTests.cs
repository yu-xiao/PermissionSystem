using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.DataPermissions;
using PermissionSystem.Domain.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.DataPermissions;

public sealed class EA020DataPermissionTests
{
    private static readonly Guid DepartmentId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ChildDepartmentId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid CustomDepartmentId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    [Theory]
    [MemberData(nameof(ScopeCases))]
    public void Filter_ShouldApplyEverySupportedScope(
        DataScopeContext context,
        IReadOnlyCollection<int> expectedIndexes)
    {
        var currentUserOrder = CreateOrder(TestIds.NormalUserId, null);
        var currentDepartmentOrder = CreateOrder(TestIds.AdminUserId, DepartmentId);
        var childDepartmentOrder = CreateOrder(TestIds.AdminUserId, ChildDepartmentId);
        var customDepartmentOrder = CreateOrder(TestIds.AdminUserId, CustomDepartmentId);
        var orders = new[]
        {
            currentUserOrder,
            currentDepartmentOrder,
            childDepartmentOrder,
            customDepartmentOrder
        };
        var specification = new DemoApprovalOrderDataPermissionSpecification();
        var result = new DataPermissionFilter()
            .Apply(
                orders.AsQueryable(),
                context,
                specification.UserIdSelector,
                specification.DepartmentIdSelector)
            .Select(entity => entity.Id)
            .ToArray();
        var expectedIds = expectedIndexes.Select(index => orders[index].Id);

        Assert.Equal(expectedIds.OrderBy(id => id), result.OrderBy(id => id));
    }

    [Fact]
    public async Task MultipleRoleScopes_ShouldUnionCurrentUserAndDepartments()
    {
        var currentUser = new TestCurrentUserService(TestIds.NormalUserId)
        {
            TenantId = TestIds.TenantId,
            DepartmentId = DepartmentId
        };
        var currentUserRole = CreateRole();
        var customRole = CreateRole();
        var service = CreateDataScopeService(
            currentUser,
            roles: [currentUserRole, customRole],
            userRoles:
            [
                CreateUserRole(currentUserRole.Id),
                CreateUserRole(customRole.Id)
            ],
            roleScopes:
            [
                CreateRoleScope(currentUserRole.Id, DataScopeType.CurrentUser),
                CreateRoleScope(customRole.Id, DataScopeType.CustomDepartments, [CustomDepartmentId])
            ]);

        var scope = await service.GetCurrentUserDataScopeAsync();

        Assert.True(scope.IncludesCurrentUser);
        Assert.False(scope.HasAllDataScope);
        Assert.Equal([CustomDepartmentId], scope.DepartmentIds);
    }

    [Fact]
    public async Task UserScope_ShouldCompletelyOverrideMergedRoleScope()
    {
        var currentUser = new TestCurrentUserService(TestIds.NormalUserId)
        {
            TenantId = TestIds.TenantId,
            DepartmentId = DepartmentId
        };
        var role = CreateRole();
        var service = CreateDataScopeService(
            currentUser,
            roles: [role],
            userRoles: [CreateUserRole(role.Id)],
            roleScopes: [CreateRoleScope(role.Id, DataScopeType.All)],
            userScopes:
            [
                new UserDataScope
                {
                    Id = Guid.NewGuid(),
                    TenantId = TestIds.TenantId,
                    UserId = TestIds.NormalUserId,
                    ScopeType = DataScopeType.CustomDepartments,
                    CustomDepartmentIds = System.Text.Json.JsonSerializer.Serialize(new[] { CustomDepartmentId })
                }
            ]);

        var scope = await service.GetCurrentUserDataScopeAsync();

        Assert.False(scope.HasAllDataScope);
        Assert.False(scope.IncludesCurrentUser);
        Assert.Equal([CustomDepartmentId], scope.DepartmentIds);
    }

    [Fact]
    public async Task UserScopeManagement_ShouldSetClearAndRotateAuthorizationStamp()
    {
        var user = new User
        {
            Id = TestIds.NormalUserId,
            TenantId = TestIds.TenantId,
            UserName = "tester",
            NormalizedUserName = "TESTER",
            DisplayName = "Tester"
        };
        var originalStamp = user.SecurityStamp;
        var userDataScopes = new InMemoryRepository<UserDataScope>();
        var service = new DataScopeService(
            new InMemoryRepository<Role>(),
            new InMemoryRepository<User>(user),
            new InMemoryRepository<UserRole>(),
            new InMemoryRepository<RoleDataScope>(),
            userDataScopes,
            new InMemoryRepository<Department>(CreateDepartment(CustomDepartmentId, "/custom/")),
            new TestCurrentUserService(TestIds.AdminUserId),
            NullLogger<DataScopeService>.Instance,
            new TestUnitOfWork());

        await service.SetUserDataScopeAsync(user.Id, new SetUserDataScopeRequest
        {
            ScopeType = DataScopeType.CustomDepartments,
            DepartmentIds = [CustomDepartmentId]
        });
        var configured = await service.GetUserDataScopeAsync(user.Id);
        var configuredStamp = user.SecurityStamp;
        await service.ClearUserDataScopeAsync(user.Id);
        var inherited = await service.GetUserDataScopeAsync(user.Id);

        Assert.True(configured.HasOverride);
        Assert.Equal(DataScopeType.CustomDepartments, configured.ScopeType);
        Assert.Equal([CustomDepartmentId], configured.DepartmentIds);
        Assert.NotEqual(originalStamp, configuredStamp);
        Assert.False(inherited.HasOverride);
        Assert.NotEqual(configuredStamp, user.SecurityStamp);
        Assert.Empty(userDataScopes.Query());
    }

    [Fact]
    public async Task CurrentDepartmentAndChildren_ShouldResolveDepartmentTree()
    {
        var currentUser = new TestCurrentUserService(TestIds.NormalUserId)
        {
            TenantId = TestIds.TenantId,
            DepartmentId = DepartmentId
        };
        var role = CreateRole();
        var service = CreateDataScopeService(
            currentUser,
            roles: [role],
            userRoles: [CreateUserRole(role.Id)],
            roleScopes: [CreateRoleScope(role.Id, DataScopeType.CurrentDepartmentAndChildren)],
            departments:
            [
                CreateDepartment(DepartmentId, "/root/"),
                CreateDepartment(ChildDepartmentId, "/root/child/")
            ]);

        var scope = await service.GetCurrentUserDataScopeAsync();

        Assert.Equal(
            new[] { DepartmentId, ChildDepartmentId }.OrderBy(id => id),
            scope.DepartmentIds.OrderBy(id => id));
    }

    [Fact]
    public async Task EmptyCustomDepartmentScope_ShouldFailClosed()
    {
        var currentUser = new TestCurrentUserService(TestIds.NormalUserId)
        {
            TenantId = TestIds.TenantId,
            DepartmentId = DepartmentId
        };
        var role = CreateRole();
        var service = CreateDataScopeService(
            currentUser,
            roles: [role],
            userRoles: [CreateUserRole(role.Id)],
            roleScopes: [CreateRoleScope(role.Id, DataScopeType.CustomDepartments, [])]);

        var scope = await service.GetCurrentUserDataScopeAsync();
        var order = CreateOrder(TestIds.NormalUserId, DepartmentId);
        var specification = new DemoApprovalOrderDataPermissionSpecification();
        var visibleOrders = new DataPermissionFilter().Apply(
            new[] { order }.AsQueryable(),
            scope,
            specification.UserIdSelector,
            specification.DepartmentIdSelector);

        Assert.False(scope.IncludesCurrentUser);
        Assert.Empty(scope.DepartmentIds);
        Assert.Empty(visibleOrders);
    }

    [Fact]
    public void ProtectedRepository_ShouldRejectWritesWithoutVisibleLookup()
    {
        var order = CreateOrder(TestIds.NormalUserId, DepartmentId);
        var repository = new DataPermissionRepository<DemoApprovalOrder>(
            new InMemoryRepository<DemoApprovalOrder>(order),
            new FixedDataScopeService(new DataScopeContext { ScopeType = DataScopeType.All }),
            new DataPermissionFilter(),
            new DemoApprovalOrderDataPermissionSpecification());

        var updateError = Assert.Throws<InvalidOperationException>(() => repository.Update(order));
        var deleteError = Assert.Throws<InvalidOperationException>(() => repository.Remove(order));

        Assert.Contains("GetVisibleByIdAsync", updateError.Message, StringComparison.Ordinal);
        Assert.Contains("GetVisibleByIdAsync", deleteError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtectedBusinessEntities_ShouldHaveSpecifications()
    {
        var domainAssembly = typeof(DemoApprovalOrder).Assembly;
        var applicationAssembly = typeof(DataPermissionRepository<>).Assembly;
        var protectedEntities = domainAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IDataPermissionEntity).IsAssignableFrom(type))
            .ToArray();

        foreach (var entityType in protectedEntities)
        {
            var specificationType = typeof(IDataPermissionSpecification<>).MakeGenericType(entityType);
            Assert.Contains(applicationAssembly.GetTypes(), type =>
                type.IsClass && !type.IsAbstract && specificationType.IsAssignableFrom(type));
        }
    }

    [Fact]
    public void ApprovalBusinessEntities_ShouldDeclareProtectionOrExemption()
    {
        var violations = typeof(IApprovalBusinessEntity).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IApprovalBusinessEntity).IsAssignableFrom(type))
            .Where(type => !typeof(IDataPermissionEntity).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttribute<DataPermissionExemptAttribute>() is null)
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void RawRepositoriesForProtectedEntities_ShouldRequireDocumentedExemption()
    {
        var applicationAssembly = typeof(DataPermissionRepository<>).Assembly;
        var violations = applicationAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !IsUnifiedRepository(type))
            .SelectMany(type => type.GetConstructors().Select(constructor => (type, constructor)))
            .Where(item => item.constructor.GetParameters().Any(parameter => IsProtectedRawRepository(parameter.ParameterType)))
            .Where(item => item.type.GetCustomAttribute<DataPermissionExemptAttribute>() is null)
            .Select(item => item.type.FullName)
            .Distinct()
            .ToArray();

        Assert.Empty(violations);
    }

    public static IEnumerable<object[]> ScopeCases()
    {
        yield return
        [
            new DataScopeContext { ScopeType = DataScopeType.All },
            new[] { 0, 1, 2, 3 }
        ];
        yield return
        [
            new DataScopeContext
            {
                ScopeType = DataScopeType.CurrentUser,
                CurrentUserId = TestIds.NormalUserId
            },
            new[] { 0 }
        ];
        yield return
        [
            new DataScopeContext
            {
                ScopeType = DataScopeType.CurrentDepartment,
                DepartmentIds = [DepartmentId]
            },
            new[] { 1 }
        ];
        yield return
        [
            new DataScopeContext
            {
                ScopeType = DataScopeType.CurrentDepartmentAndChildren,
                DepartmentIds = [DepartmentId, ChildDepartmentId]
            },
            new[] { 1, 2 }
        ];
        yield return
        [
            new DataScopeContext
            {
                ScopeType = DataScopeType.CustomDepartments,
                DepartmentIds = [CustomDepartmentId]
            },
            new[] { 3 }
        ];
    }

    private static DataScopeService CreateDataScopeService(
        TestCurrentUserService currentUser,
        IReadOnlyCollection<Role>? roles = null,
        IReadOnlyCollection<UserRole>? userRoles = null,
        IReadOnlyCollection<RoleDataScope>? roleScopes = null,
        IReadOnlyCollection<UserDataScope>? userScopes = null,
        IReadOnlyCollection<Department>? departments = null)
    {
        return new DataScopeService(
            new InMemoryRepository<Role>((roles ?? []).ToArray()),
            new InMemoryRepository<User>(),
            new InMemoryRepository<UserRole>((userRoles ?? []).ToArray()),
            new InMemoryRepository<RoleDataScope>((roleScopes ?? []).ToArray()),
            new InMemoryRepository<UserDataScope>((userScopes ?? []).ToArray()),
            new InMemoryRepository<Department>((departments ?? []).ToArray()),
            currentUser,
            NullLogger<DataScopeService>.Instance,
            new TestUnitOfWork());
    }

    private static DemoApprovalOrder CreateOrder(Guid userId, Guid? departmentId)
    {
        return new DemoApprovalOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ApplicantUserId = userId,
            DepartmentId = departmentId
        };
    }

    private static Role CreateRole()
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = Guid.NewGuid().ToString("N"),
            Name = "Role",
            IsEnabled = true
        };
    }

    private static UserRole CreateUserRole(Guid roleId)
    {
        return new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            UserId = TestIds.NormalUserId,
            RoleId = roleId
        };
    }

    private static RoleDataScope CreateRoleScope(
        Guid roleId,
        DataScopeType scopeType,
        IReadOnlyCollection<Guid>? departmentIds = null)
    {
        return new RoleDataScope
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            RoleId = roleId,
            ScopeType = scopeType,
            CustomDepartmentIds = departmentIds is null
                ? null
                : System.Text.Json.JsonSerializer.Serialize(departmentIds)
        };
    }

    private static Department CreateDepartment(Guid id, string treePath)
    {
        return new Department
        {
            Id = id,
            TenantId = TestIds.TenantId,
            Name = id.ToString("N"),
            Code = id.ToString("N"),
            TreePath = treePath
        };
    }

    private static bool IsUnifiedRepository(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DataPermissionRepository<>);
    }

    private sealed class FixedDataScopeService : IDataScopeService
    {
        private readonly DataScopeContext _context;

        public FixedDataScopeService(DataScopeContext context)
        {
            _context = context;
        }

        public Task<DataScopeContext> GetCurrentUserDataScopeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_context);
        }

        public Task<RoleDataScopeResponse> GetRoleDataScopeAsync(
            Guid roleId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetRoleDataScopeAsync(
            Guid roleId,
            SetRoleDataScopeRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private static bool IsProtectedRawRepository(Type parameterType)
    {
        return parameterType.IsGenericType &&
            parameterType.GetGenericTypeDefinition() == typeof(IRepository<>) &&
            typeof(IDataPermissionEntity).IsAssignableFrom(parameterType.GetGenericArguments()[0]);
    }
}
