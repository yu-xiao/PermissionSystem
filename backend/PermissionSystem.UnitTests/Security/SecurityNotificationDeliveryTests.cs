using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Application.Notifications;
using PermissionSystem.Application.Security;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Security;

public sealed class SecurityNotificationDeliveryTests
{
    [Fact]
    public async Task SendVerification_ShouldRejectDisabledDeliveryBeforePersistingCode()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        var verifications = new InMemoryRepository<SensitiveOperationVerification>();
        var notificationService = new TestNotificationService
        {
            DeliveryMode = NotificationDeliveryMode.Disabled
        };
        var service = new SecurityPolicyService(
            new InMemoryRepository<SecurityPolicy>(),
            new InMemoryRepository<LoginFailureRecord>(),
            verifications,
            new InMemoryRepository<IpAccessRule>(),
            tenantContext,
            new TestCurrentUserService { TenantId = TestIds.TenantId },
            new TestSensitiveOperationCodeProvider(),
            notificationService,
            NullLogger<SecurityPolicyService>.Instance,
            new TestUnitOfWork());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendVerificationAsync(new SendSensitiveVerificationRequest
            {
                OperationCode = "security:test"
            }));

        Assert.Equal(ErrorCode.BusinessError, exception.ErrorCode);
        Assert.Empty(verifications.Items);
        Assert.Empty(notificationService.Sent);
    }

    [Theory]
    [InlineData(NotificationDeliveryMode.Direct)]
    [InlineData(NotificationDeliveryMode.OutboxRabbitMQ)]
    public async Task SendVerification_ShouldEnqueueNotificationAndVerificationInOneTransaction(
        NotificationDeliveryMode deliveryMode)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TestIds.TenantId, "Test");
        var verifications = new InMemoryRepository<SensitiveOperationVerification>();
        var unitOfWork = new TestUnitOfWork();
        var notificationService = new TestNotificationService
        {
            DeliveryMode = deliveryMode
        };
        var service = new SecurityPolicyService(
            new InMemoryRepository<SecurityPolicy>(),
            new InMemoryRepository<LoginFailureRecord>(),
            verifications,
            new InMemoryRepository<IpAccessRule>(),
            tenantContext,
            new TestCurrentUserService { TenantId = TestIds.TenantId, UserId = TestIds.NormalUserId },
            new TestSensitiveOperationCodeProvider(),
            notificationService,
            NullLogger<SecurityPolicyService>.Instance,
            unitOfWork);

        var response = await service.SendVerificationAsync(new SendSensitiveVerificationRequest
        {
            OperationCode = "security:test"
        });

        Assert.Equal("security:test", response.OperationCode);
        Assert.Single(verifications.Items);
        Assert.Single(notificationService.Sent);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Contains(
            deliveryMode == NotificationDeliveryMode.OutboxRabbitMQ ? "queued" : "delivered",
            response.DeliveryMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestSensitiveOperationCodeProvider : ISensitiveOperationCodeProvider
    {
        public string? VerificationCode => null;
    }
}
