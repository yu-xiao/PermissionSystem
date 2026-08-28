using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class OpenAiCompatibleModelClientTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsContentAndUsage()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-secret", request.Headers.Authorization?.Parameter);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, """
                {
                  "model": "test-model",
                  "choices": [{ "message": { "content": "verified answer" } }],
                  "usage": { "prompt_tokens": 10, "completion_tokens": 4, "total_tokens": 14 }
                }
                """));
        });
        var client = CreateClient(handler);

        var response = await client.CompleteAsync(ValidRequest());

        Assert.Equal("verified answer", response.Content);
        Assert.Equal("test-model", response.Model);
        Assert.Equal(14, response.TotalTokens);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "provider_rate_limited", ErrorCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.BadGateway, "provider_unavailable", ErrorCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadRequest, "provider_request_rejected", ErrorCode.BadRequest, false)]
    public async Task CompleteAsync_MapsProviderErrorsWithoutResponseBody(
        HttpStatusCode statusCode,
        string errorType,
        ErrorCode errorCode,
        bool isTransient)
    {
        const string sensitiveBody = "provider leaked token test-secret";
        var client = CreateClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(statusCode, sensitiveBody))));

        var exception = await Assert.ThrowsAsync<AiModelGatewayException>(() =>
            client.CompleteAsync(ValidRequest()));

        Assert.Equal(errorType, exception.ErrorType);
        Assert.Equal(errorCode, exception.ErrorCode);
        Assert.Equal(isTransient, exception.IsTransient);
        Assert.DoesNotContain("test-secret", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_PreservesCallerCancellation()
    {
        var client = CreateClient(new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return JsonResponse(HttpStatusCode.OK, "{}");
        }));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.CompleteAsync(ValidRequest(), cancellationSource.Token));
    }

    [Fact]
    public async Task CompleteAsync_MapsConfiguredTimeout()
    {
        var client = CreateClient(new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, "{}");
        }), timeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<AiModelGatewayException>(() =>
            client.CompleteAsync(ValidRequest()));

        Assert.Equal("provider_timeout", exception.ErrorType);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task CompleteAsync_BlocksPrivateEndpointByDefault()
    {
        var client = CreateClient(
            new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP must not be called.")),
            allowPrivateNetwork: false);

        var exception = await Assert.ThrowsAsync<AiModelGatewayException>(() =>
            client.CompleteAsync(ValidRequest()));

        Assert.Equal("provider_endpoint_blocked", exception.ErrorType);
        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
    }

    private static OpenAiCompatibleModelClient CreateClient(
        HttpMessageHandler handler,
        int timeoutSeconds = 5,
        bool allowPrivateNetwork = true)
    {
        var options = Options.Create(new OpenAiCompatibleOptions
        {
            Enabled = true,
            BaseUrl = "http://localhost/",
            ChatCompletionsPath = "v1/chat/completions",
            ApiKey = "test-secret",
            Model = "test-model",
            TimeoutSeconds = timeoutSeconds,
            AllowInsecureHttp = true,
            AllowPrivateNetwork = allowPrivateNetwork,
            AllowedHosts = ["localhost"]
        });
        return new OpenAiCompatibleModelClient(new HttpClient(handler), options);
    }

    private static AiChatCompletionRequest ValidRequest()
    {
        return new AiChatCompletionRequest
        {
            Messages = [new AiChatMessage("user", "hello")]
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
