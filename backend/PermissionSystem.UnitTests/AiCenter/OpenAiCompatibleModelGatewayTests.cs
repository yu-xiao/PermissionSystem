using System.Net;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class OpenAiCompatibleModelGatewayTests
{
    [Fact]
    public async Task CompleteAsync_SendsToolsAndReturnsToolCalls()
    {
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-secret", request.Headers.Authorization?.Parameter);

            var requestJson = await request.Content!.ReadAsStringAsync();
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            Assert.False(root.GetProperty("parallel_tool_calls").GetBoolean());
            Assert.Equal("auto", root.GetProperty("tool_choice").GetString());
            Assert.Equal("search_users", root.GetProperty("tools")[0]
                .GetProperty("function").GetProperty("name").GetString());

            return JsonResponse(HttpStatusCode.OK, """
                {
                  "id": "request-1",
                  "model": "test-model",
                  "choices": [{
                    "finish_reason": "tool_calls",
                    "message": {
                      "content": null,
                      "tool_calls": [{
                        "id": "call-1",
                        "type": "function",
                        "function": { "name": "search_users", "arguments": "{\"keyword\":\"alice\"}" }
                      }]
                    }
                  }],
                  "usage": { "prompt_tokens": 12, "completion_tokens": 6, "total_tokens": 18 }
                }
                """);
        });
        var gateway = CreateGateway(handler);

        var response = await gateway.CompleteAsync(Provider(), new AiModelGatewayRequest
        {
            Messages = [new AiModelGatewayMessage { Role = "user", Content = "find alice" }],
            Tools =
            [
                new AiModelToolDefinition
                {
                    Name = "search_users",
                    Description = "Search users.",
                    ParametersJson = """{"type":"object","properties":{"keyword":{"type":"string"}}}"""
                }
            ]
        });

        var toolCall = Assert.Single(response.ToolCalls);
        Assert.Equal("call-1", toolCall.Id);
        Assert.Equal("search_users", toolCall.Name);
        Assert.Equal("{\"keyword\":\"alice\"}", toolCall.ArgumentsJson);
        Assert.Equal("request-1", response.ProviderRequestId);
        Assert.Equal(18, response.TotalTokens);
    }

    [Fact]
    public async Task CompleteAsync_RejectsResponseLargerThanLimit()
    {
        var oversizedBody = "{\"padding\":\"" + new string('x', 1024 * 1024) + "\"}";
        var gateway = CreateGateway(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, oversizedBody))));

        var exception = await Assert.ThrowsAsync<AiModelGatewayException>(() =>
            gateway.CompleteAsync(Provider(), ValidRequest()));

        Assert.Equal("provider_response_invalid", exception.ErrorType);
    }

    [Fact]
    public async Task CompleteAsync_RejectsMalformedToolArguments()
    {
        var gateway = CreateGateway(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """
                {
                  "choices": [{
                    "message": {
                      "tool_calls": [{
                        "id": "call-1",
                        "function": { "name": "search_users", "arguments": "not-json" }
                      }]
                    }
                  }]
                }
                """))));

        var exception = await Assert.ThrowsAsync<AiModelGatewayException>(() =>
            gateway.CompleteAsync(Provider(), ValidRequest()));

        Assert.Equal("provider_response_invalid", exception.ErrorType);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "provider_rate_limited", ErrorCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.BadGateway, "provider_unavailable", ErrorCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadRequest, "provider_request_rejected", ErrorCode.BadRequest, false)]
    public async Task CompleteAsync_MapsProviderErrorsWithoutReadingSensitiveBody(
        HttpStatusCode statusCode,
        string errorType,
        ErrorCode errorCode,
        bool isTransient)
    {
        const string sensitiveBody = "provider leaked token test-secret";
        var gateway = CreateGateway(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(statusCode, sensitiveBody))));

        var exception = await Assert.ThrowsAsync<AiModelGatewayException>(() =>
            gateway.CompleteAsync(Provider(), ValidRequest()));

        Assert.Equal(errorType, exception.ErrorType);
        Assert.Equal(errorCode, exception.ErrorCode);
        Assert.Equal(isTransient, exception.IsTransient);
        Assert.DoesNotContain(sensitiveBody, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", exception.ToString(), StringComparison.Ordinal);
    }

    private static OpenAiCompatibleModelGateway CreateGateway(HttpMessageHandler handler)
    {
        return new OpenAiCompatibleModelGateway(
            new StubHttpClientFactory(new HttpClient(handler)));
    }

    private static AiProviderConnectionSettings Provider()
    {
        return new AiProviderConnectionSettings
        {
            BaseUrl = "http://localhost/",
            ChatCompletionsPath = "v1/chat/completions",
            ApiKey = "test-secret",
            ModelName = "test-model",
            TimeoutSeconds = 5,
            AllowInsecureHttp = true,
            AllowPrivateNetwork = true,
            AllowedHosts = ["localhost"]
        };
    }

    private static AiModelGatewayRequest ValidRequest()
    {
        return new AiModelGatewayRequest
        {
            Messages = [new AiModelGatewayMessage { Role = "user", Content = "hello" }]
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
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
