using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiOperationsServiceTests
{
    [Fact]
    public async Task SaveMyFeedbackAsync_UpsertsOwnedCompletedRun()
    {
        var responseMessageId = Guid.NewGuid();
        var run = new AiRun
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ActorUserId = TestIds.NormalUserId,
            ResponseMessageId = responseMessageId,
            Status = AiRunStatus.Completed
        };
        var feedback = new InMemoryRepository<AiUserFeedback>();
        var service = CreateService(feedback, run);

        await service.SaveMyFeedbackAsync(run.Id, new SaveAiFeedbackRequest
        {
            Rating = AiFeedbackRating.Negative,
            ReasonCode = "incorrect"
        });
        var updated = await service.SaveMyFeedbackAsync(run.Id, new SaveAiFeedbackRequest
        {
            Rating = AiFeedbackRating.Positive
        });

        Assert.Single(feedback.Items);
        Assert.Equal(AiFeedbackRating.Positive, updated.Rating);
    }

    [Fact]
    public async Task SaveMyFeedbackAsync_ForAnotherUsersRunIsNotFound()
    {
        var run = new AiRun
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ActorUserId = TestIds.AdminUserId,
            ResponseMessageId = Guid.NewGuid(),
            Status = AiRunStatus.Completed
        };
        var service = CreateService(new InMemoryRepository<AiUserFeedback>(), run);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SaveMyFeedbackAsync(
            run.Id,
            new SaveAiFeedbackRequest { Rating = AiFeedbackRating.Positive }));

        Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
    }

    private static AiOperationsService CreateService(
        InMemoryRepository<AiUserFeedback> feedback,
        AiRun run)
    {
        return new AiOperationsService(
            feedback,
            new InMemoryRepository<AiRun>(run),
            new InMemoryRepository<AiUsageLog>(),
            new InMemoryRepository<AiProviderConfig>(),
            new InMemoryAsyncQueryExecutor(),
            new TestCurrentUserService(TestIds.NormalUserId),
            new TestUnitOfWork());
    }
}
