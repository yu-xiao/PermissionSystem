using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Reports;
using PermissionSystem.Application.Roles;
using PermissionSystem.Application.ScheduledTasks;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.Sso;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.ProductGovernance;

public sealed class EA029ProductStateGovernanceTests
{
    [Fact]
    public async Task ReservedSsoProviders_ShouldBeReadOnlyAndExcludedFromEnabledProviders()
    {
        var oidc = CreateSsoProvider(SsoProviderType.Oidc, "oidc");
        var saml = CreateSsoProvider(SsoProviderType.Saml, "saml");
        var oauth2 = CreateSsoProvider(SsoProviderType.OAuth2, "oauth2");
        var service = CreateSsoService(oidc, saml, oauth2);

        var enabled = await service.GetEnabledAsync();

        Assert.Equal(oidc.Id, Assert.Single(enabled).Id);
        await AssertValidationFailedAsync(() => service.CreateAsync(new CreateSsoProviderRequest
        {
            TenantId = TestIds.TenantId,
            ProviderCode = "new-saml",
            ProviderName = "New SAML",
            ProviderType = SsoProviderType.Saml,
            AllowLocalLoginFallback = true
        }));
        await AssertValidationFailedAsync(() => service.UpdateAsync(saml.Id, new UpdateSsoProviderRequest
        {
            ProviderName = saml.ProviderName,
            ProviderType = SsoProviderType.Saml,
            AllowLocalLoginFallback = true
        }));
        await AssertValidationFailedAsync(() => service.UpdateAsync(oidc.Id, new UpdateSsoProviderRequest
        {
            ProviderName = oidc.ProviderName,
            ProviderType = SsoProviderType.OAuth2,
            Authority = oidc.Authority,
            ClientId = oidc.ClientId,
            CallbackPath = oidc.CallbackPath,
            ResponseType = oidc.ResponseType,
            AllowLocalLoginFallback = true
        }));
        await AssertValidationFailedAsync(() => service.SetEnabledAsync(saml.Id, true));
        await AssertValidationFailedAsync(() => service.TestAsync(oauth2.Id, new TestSsoProviderRequest()));
    }

    [Fact]
    public async Task ApiReportDefinition_ShouldBeRejectedAtCreation()
    {
        var service = CreateReportService(new InMemoryRepository<ReportDefinition>(), new RecordingReportQueryExecutor());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(new CreateReportDefinitionRequest
        {
            ReportCode = "api-report",
            ReportName = "API report",
            Category = "Reserved",
            DataSourceType = "Api",
            ApiUrl = "https://example.test/report"
        }));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task ReservedApiReport_ShouldNotExecuteOrExport()
    {
        var definition = new ReportDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ReportCode = "legacy-api",
            ReportName = "Legacy API",
            Category = "Reserved",
            DataSourceType = "Api",
            ApiUrl = "https://example.test/report",
            IsEnabled = true
        };
        var executor = new RecordingReportQueryExecutor();
        var service = CreateReportService(new InMemoryRepository<ReportDefinition>(definition), executor);

        var queryException = await Assert.ThrowsAsync<BusinessException>(() =>
            service.QueryAsync(definition.Id, new ReportQueryRequest()));
        var exportException = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ExportAsync(definition.Id, new ReportQueryRequest()));

        Assert.Equal(ErrorCode.Conflict, queryException.ErrorCode);
        Assert.Equal(ErrorCode.Conflict, exportException.ErrorCode);
        Assert.Equal(0, executor.ExecutionCount);
    }

    [Fact]
    public async Task SecurityPolicy_ShouldExposeAndPersistReservedCapabilitiesAsDisabled()
    {
        var policy = new SecurityPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            PasswordExpireDays = 90,
            EnableMfa = true
        };
        var repository = new InMemoryRepository<SecurityPolicy>(policy);
        var service = CreateSecurityPolicyService(repository);

        var current = await service.GetPolicyAsync();
        var updated = await service.UpdatePolicyAsync(new UpdateSecurityPolicyRequest
        {
            PasswordExpireDays = 180,
            EnableMfa = true
        });

        Assert.Equal(0, current.PasswordExpireDays);
        Assert.False(current.EnableMfa);
        Assert.Equal(0, updated.PasswordExpireDays);
        Assert.False(updated.EnableMfa);
        Assert.Equal(0, policy.PasswordExpireDays);
        Assert.False(policy.EnableMfa);
    }

    [Fact]
    public async Task CustomScheduledTask_ShouldBeRejectedAtCreation()
    {
        var service = CreateScheduledTaskService(
            new InMemoryRepository<ScheduledTask>(),
            new RecordingBackgroundJobService());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(new CreateScheduledTaskRequest
        {
            TenantId = TestIds.TenantId,
            Code = "custom-job",
            Name = "Custom job",
            JobType = "CustomHandler",
            CronExpression = "* * * * *",
            Queue = "default"
        }));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task DemoScheduledTask_ShouldRegisterOnlyControlledJobImplementation()
    {
        var backgroundJobs = new RecordingBackgroundJobService();
        var service = CreateScheduledTaskService(new InMemoryRepository<ScheduledTask>(), backgroundJobs);

        await service.CreateAsync(new CreateScheduledTaskRequest
        {
            TenantId = TestIds.TenantId,
            Code = "demo-job",
            Name = "Demo job",
            JobType = ScheduledTaskJobTypes.DemoLog,
            CronExpression = "* * * * *",
            Queue = "default",
            IsEnabled = true
        });

        Assert.Equal(typeof(DemoScheduledTaskJob), Assert.Single(backgroundJobs.RegisteredJobTypes));
    }

    [Fact]
    public async Task Synchronization_ShouldRemoveReservedHistoricalTaskWithoutRegisteringIt()
    {
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "legacy-job",
            Name = "Legacy job",
            JobType = "LegacyHandler",
            CronExpression = "* * * * *",
            Queue = "default",
            IsEnabled = true
        };
        var backgroundJobs = new RecordingBackgroundJobService();
        var service = CreateScheduledTaskService(
            new InMemoryRepository<ScheduledTask>(task),
            backgroundJobs);

        await service.SyncEnabledTasksAsync();

        Assert.Empty(backgroundJobs.RegisteredJobTypes);
        Assert.Equal(
            ScheduledTaskService.GetRecurringJobId(task.Id),
            Assert.Single(backgroundJobs.RemovedRecurringJobIds));
    }

    [Fact]
    public async Task ReservedHistoricalScheduledTask_ShouldRemainReadOnly()
    {
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "legacy-job",
            Name = "Legacy job",
            JobType = "LegacyHandler",
            CronExpression = "* * * * *",
            Queue = "default",
            IsEnabled = false
        };
        var service = CreateScheduledTaskService(
            new InMemoryRepository<ScheduledTask>(task),
            new RecordingBackgroundJobService());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(
            task.Id,
            new UpdateScheduledTaskRequest
            {
                Name = task.Name,
                JobType = ScheduledTaskJobTypes.DemoLog,
                CronExpression = task.CronExpression,
                Queue = task.Queue,
                IsEnabled = false
            }));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
        Assert.Equal("LegacyHandler", task.JobType);
    }

    [Fact]
    public async Task FieldPermissions_ShouldBeRejectedBeforeAuthorizationMatrixTransaction()
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Code = "operator",
            Name = "Operator"
        };
        var menu = new Menu
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            Name = "Orders"
        };
        var roleMenus = new InMemoryRepository<RoleMenu>();
        var rolePermissions = new InMemoryRepository<RolePermission>();
        var unitOfWork = new TestUnitOfWork();
        var service = new RoleService(
            new InMemoryRepository<Role>(role),
            roleMenus,
            rolePermissions,
            new InMemoryRepository<Menu>(menu),
            new InMemoryRepository<Domain.Entities.Permission>(),
            new InMemoryRepository<User>(),
            new InMemoryRepository<UserRole>(),
            new InMemoryRepository<RoleDataScope>(),
            new InMemoryRepository<Department>(),
            new TestCurrentUserService(),
            new TestTenantWriteResolver(),
            new TestCacheService(),
            new TestSecurityPolicyService(),
            NullLogger<RoleService>.Instance,
            unitOfWork,
            new InMemoryAsyncQueryExecutor());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SavePermissionMatrixAsync(role.Id, new SaveRolePermissionMatrixRequest
            {
                FieldPermissions =
                [
                    new RoleFieldPermissionRequest
                    {
                        MenuId = menu.Id,
                        FieldCode = "unitPrice",
                        Visible = true,
                        Editable = false,
                        Masked = true
                    }
                ]
            }));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
        Assert.Equal(0, unitOfWork.TransactionCount);
        Assert.Empty(roleMenus.Items);
        Assert.Empty(rolePermissions.Items);
    }

    private static SsoProviderService CreateSsoService(params SsoProvider[] providers)
    {
        return new SsoProviderService(
            new InMemoryRepository<SsoProvider>(providers),
            new InMemoryRepository<SsoUserBinding>(),
            new TestConfigValueProtector(),
            new TestTenantWriteResolver(),
            new TestUnitOfWork());
    }

    private static SsoProvider CreateSsoProvider(SsoProviderType providerType, string code)
    {
        return new SsoProvider
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ProviderCode = code,
            ProviderName = code.ToUpperInvariant(),
            ProviderType = providerType,
            Enabled = true,
            Authority = "https://identity.example.test",
            ClientId = "client-id"
        };
    }

    private static ReportService CreateReportService(
        InMemoryRepository<ReportDefinition> definitions,
        RecordingReportQueryExecutor executor)
    {
        return new ReportService(
            definitions,
            new InMemoryRepository<ReportQueryParam>(),
            new InMemoryRepository<ReportExecutionLog>(),
            executor,
            new TestExcelService(),
            new TestCurrentUserService(),
            new TestUnitOfWork(),
            new TestReportDatasetCatalog(),
            new InMemoryAsyncQueryExecutor());
    }

    private static SecurityPolicyService CreateSecurityPolicyService(InMemoryRepository<SecurityPolicy> policies)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        return new SecurityPolicyService(
            policies,
            new InMemoryRepository<LoginFailureRecord>(),
            new InMemoryRepository<SensitiveOperationVerification>(),
            new InMemoryRepository<User>(),
            new InMemoryRepository<IpAccessRule>(),
            tenantContext,
            new TestCurrentUserService(),
            new TestSensitiveOperationCodeProvider(),
            new TestPasswordHashService(),
            new PermissiveStepUpVerificationStore(),
            NullLogger<SecurityPolicyService>.Instance,
            new TestUnitOfWork());
    }

    private static ScheduledTaskService CreateScheduledTaskService(
        InMemoryRepository<ScheduledTask> tasks,
        RecordingBackgroundJobService backgroundJobs)
    {
        return new ScheduledTaskService(
            tasks,
            new InMemoryRepository<ScheduledTaskExecutionLog>(),
            backgroundJobs,
            new TestTenantWriteResolver(),
            new TestUnitOfWork(),
            new TestSystemTenantScope(),
            new ActiveTenantStatusChecker());
    }

    private static async Task AssertValidationFailedAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(action);
        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    private sealed class RecordingReportQueryExecutor : IReportQueryExecutor
    {
        public int ExecutionCount { get; private set; }

        public Task<ReportExecutionResult> ExecuteAsync(
            ReportExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(new ReportExecutionResult());
        }
    }

    private sealed class TestReportDatasetCatalog : IReportDatasetCatalog
    {
        public IReadOnlyList<ReportDatasetResponse> GetAvailable() => [];

        public ReportDatasetDefinition GetRequired(string datasetKey)
        {
            return new ReportDatasetDefinition { Key = datasetKey };
        }
    }

    private sealed class TestSensitiveOperationCodeProvider : ISensitiveOperationCodeProvider
    {
        public string? StepUpTicket => "test-ticket";
    }

    private sealed class PermissiveStepUpVerificationStore : IStepUpVerificationStore
    {
        public Task<bool> RegisterFailedAttemptAsync(
            Guid id,
            int maxAttempts,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> MarkVerifiedAsync(
            Guid id,
            string ticketHash,
            DateTimeOffset verifiedAt,
            DateTimeOffset ticketExpiresAt,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> TryConsumeTicketAsync(
            Guid tenantId,
            Guid userId,
            string sessionId,
            string operationCode,
            string ticketHash,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RecordingBackgroundJobService : IBackgroundJobService
    {
        public List<Type> RegisteredJobTypes { get; } = [];

        public List<string> RemovedRecurringJobIds { get; } = [];

        public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) => "test-job";

        public string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) => "test-job";

        public void AddOrUpdateRecurring<TJob>(
            string recurringJobId,
            Expression<Func<TJob, Task>> methodCall,
            string cronExpression,
            TimeZoneInfo? timeZone = null,
            string queue = "default")
        {
            RegisteredJobTypes.Add(typeof(TJob));
        }

        public void RemoveRecurring(string recurringJobId)
        {
            RemovedRecurringJobIds.Add(recurringJobId);
        }

        public void TriggerRecurring(string recurringJobId)
        {
        }

        public bool Delete(string jobId) => true;
    }

    private sealed class TestSystemTenantScope : ISystemTenantScope
    {
        public IDisposable Begin(string operation) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class ActiveTenantStatusChecker : ITenantStatusChecker
    {
        public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
