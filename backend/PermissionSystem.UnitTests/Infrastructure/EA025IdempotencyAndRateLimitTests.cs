using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PermissionSystem.Api.Idempotency;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure.Idempotency;
using PermissionSystem.Infrastructure.RateLimiting;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.Infrastructure;

public sealed class EA025IdempotencyAndRateLimitTests
{
    [Fact]
    public async Task IdempotencyFilter_SameKeyAndSameRequest_ReplaysStoredResponse()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var idempotencyService = new MemoryIdempotencyService(memoryCache);
        var filter = CreateFilter(idempotencyService);
        var executionCount = 0;

        var first = CreateActionContext("{\"name\":\"A\"}", "same-key");
        await filter.OnResourceExecutionAsync(first, () =>
        {
            executionCount++;
            return Task.FromResult(new ResourceExecutedContext(first, [])
            {
                Result = new OkObjectResult(new { Created = true })
            });
        });

        var second = CreateActionContext("{\"name\":\"A\"}", "same-key");
        await filter.OnResourceExecutionAsync(second, () =>
        {
            executionCount++;
            return Task.FromResult(new ResourceExecutedContext(second, []));
        });

        var replay = Assert.IsType<ContentResult>(second.Result);
        Assert.Equal(1, executionCount);
        Assert.Equal(StatusCodes.Status200OK, replay.StatusCode);
        Assert.Contains("created", replay.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IdempotencyFilter_SameKeyAndDifferentBody_ReturnsConflict()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var idempotencyService = new MemoryIdempotencyService(memoryCache);
        var filter = CreateFilter(idempotencyService);

        var first = CreateActionContext("{\"name\":\"A\"}", "same-key");
        await filter.OnResourceExecutionAsync(first, () => Task.FromResult(
            new ResourceExecutedContext(first, []) { Result = new OkObjectResult(new { Created = true }) }));

        var second = CreateActionContext("{\"name\":\"B\"}", "same-key");
        await filter.OnResourceExecutionAsync(second, () => throw new Xunit.Sdk.XunitException("The action must not execute."));

        var conflict = Assert.IsType<ConflictObjectResult>(second.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task IdempotencyFilter_SameKeyAndDifferentPath_ReturnsConflict()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var idempotencyService = new MemoryIdempotencyService(memoryCache);
        var filter = CreateFilter(idempotencyService);

        var first = CreateActionContext("{\"name\":\"A\"}", "same-key", "/api/orders");
        await filter.OnResourceExecutionAsync(first, () => Task.FromResult(
            new ResourceExecutedContext(first, []) { Result = new OkObjectResult(new { Created = true }) }));

        var second = CreateActionContext("{\"name\":\"A\"}", "same-key", "/api/orders/next");
        await filter.OnResourceExecutionAsync(second, () => throw new Xunit.Sdk.XunitException("The action must not execute."));

        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public async Task MemoryDistributedRateLimitService_InstancesShareWindowWithinProcess()
    {
        var service = new MemoryDistributedRateLimitService();
        var first = await service.TryAcquireAsync("api-key", "client-a", 2, TimeSpan.FromMinutes(1));
        var second = await service.TryAcquireAsync("api-key", "client-a", 2, TimeSpan.FromMinutes(1));
        var third = await service.TryAcquireAsync("api-key", "client-a", 2, TimeSpan.FromMinutes(1));

        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
        Assert.False(third.IsAcquired);
        Assert.True(third.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task MemoryIdempotencyService_OnlyOwnerCanCompleteOrRemoveEntry()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new MemoryIdempotencyService(memoryCache);
        var processing = new IdempotencyCacheEntry
        {
            State = "Processing",
            OperationId = "owner",
            Method = "POST",
            Path = "/api/orders",
            RequestBodyHash = "request-hash"
        };

        Assert.True(await service.TryBeginAsync("key", processing, TimeSpan.FromMinutes(1)));
        Assert.False(await service.StoreAsync("key", "other", CreateCompletedEntry(processing), TimeSpan.FromMinutes(1)));
        await service.RemoveAsync("key", "other");
        Assert.NotNull(await service.GetAsync("key"));

        Assert.True(await service.StoreAsync("key", "owner", CreateCompletedEntry(processing), TimeSpan.FromMinutes(1)));
        Assert.Equal("Completed", (await service.GetAsync("key"))!.State);
    }

    private static IdempotencyFilter CreateFilter(IIdempotencyService idempotencyService)
    {
        return new IdempotencyFilter(
            idempotencyService,
            new TestCurrentUserService(),
            NullLogger<IdempotencyFilter>.Instance);
    }

    private static ResourceExecutingContext CreateActionContext(string body, string idempotencyKey, string path = "/api/orders")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = path;
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.ContentLength = httpContext.Request.Body.Length;
        httpContext.Request.Headers["X-Idempotency-Key"] = idempotencyKey;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var actionDescriptor = new ActionDescriptor
        {
            EndpointMetadata = [new IdempotencyKeyAttribute()]
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor);
        var filters = new List<IFilterMetadata>();
        return new ResourceExecutingContext(
            actionContext,
            filters,
            new List<IValueProviderFactory>());
    }

    private static IdempotencyCacheEntry CreateCompletedEntry(IdempotencyCacheEntry processing)
    {
        return new IdempotencyCacheEntry
        {
            State = "Completed",
            OperationId = processing.OperationId,
            Method = processing.Method,
            Path = processing.Path,
            RequestBodyHash = processing.RequestBodyHash
        };
    }
}
