using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class OpenAiCompatibleModelGateway : IAiModelGateway
{
    private const int MaxResponseBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiCompatibleModelGateway(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AiModelGatewayResponse> CompleteAsync(
        AiProviderConnectionSettings provider,
        AiModelGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var options = ToOptions(provider);
        var endpoint = OpenAiCompatibleEndpointValidator.ValidateConfiguration(options);
        await OpenAiCompatibleEndpointValidator.ValidateResolvedAddressesAsync(
            endpoint,
            provider.AllowPrivateNetwork,
            cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(provider.TimeoutSeconds));
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        message.Content = JsonContent.Create(CreatePayload(provider, request), options: JsonOptions);

        try
        {
            var client = _httpClientFactory.CreateClient("AiModelGateway");
            using var response = await client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw MapHttpError(response.StatusCode);
            }

            if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            {
                throw InvalidResponse();
            }

            var payload = await ReadPayloadAsync(response.Content, timeoutSource.Token);
            var choice = payload?.Choices?.FirstOrDefault();
            var responseToolCalls = choice?.Message?.ToolCalls ?? [];
            if (choice?.Message is null ||
                (string.IsNullOrWhiteSpace(choice.Message.Content) && responseToolCalls.Count == 0))
            {
                throw InvalidResponse();
            }

            var toolCalls = responseToolCalls.Select(call => new AiModelToolCall
            {
                Id = call.Id,
                Name = call.Function.Name,
                ArgumentsJson = string.IsNullOrWhiteSpace(call.Function.Arguments) ? "{}" : call.Function.Arguments
            }).ToList();
            if (toolCalls.Any(call =>
                    string.IsNullOrWhiteSpace(call.Id) ||
                    string.IsNullOrWhiteSpace(call.Name) ||
                    !IsJsonObject(call.ArgumentsJson)))
            {
                throw InvalidResponse();
            }

            return new AiModelGatewayResponse
            {
                Content = choice.Message.Content,
                ToolCalls = toolCalls,
                Model = string.IsNullOrWhiteSpace(payload!.Model) ? provider.ModelName : payload.Model,
                ProviderRequestId = payload.Id,
                FinishReason = choice.FinishReason,
                InputTokens = payload.Usage?.PromptTokens,
                OutputTokens = payload.Usage?.CompletionTokens,
                TotalTokens = payload.Usage?.TotalTokens
            };
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiModelGatewayException(
                "provider_timeout",
                ErrorCode.InternalServerError,
                "The AI provider request timed out.",
                true,
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiModelGatewayException(
                "provider_unavailable",
                ErrorCode.InternalServerError,
                "The AI provider is unavailable.",
                true,
                innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new AiModelGatewayException(
                "provider_response_invalid",
                ErrorCode.InternalServerError,
                "The AI provider returned an invalid response.",
                false,
                innerException: exception);
        }
    }

    private static object CreatePayload(
        AiProviderConnectionSettings provider,
        AiModelGatewayRequest request)
    {
        var messages = request.Messages.Select(item => new
        {
            role = item.Role,
            content = item.Content,
            tool_call_id = item.ToolCallId,
            tool_calls = item.ToolCalls.Count == 0
                ? null
                : item.ToolCalls.Select(call => new
                {
                    id = call.Id,
                    type = "function",
                    function = new { name = call.Name, arguments = call.ArgumentsJson }
                })
        });
        var tools = request.Tools.Select(tool => new
        {
            type = "function",
            function = new
            {
                name = tool.Name,
                description = tool.Description,
                parameters = JsonSerializer.Deserialize<JsonElement>(tool.ParametersJson, JsonOptions)
            }
        });

        return new
        {
            model = provider.ModelName,
            messages,
            tools = request.Tools.Count == 0 ? null : tools,
            tool_choice = request.Tools.Count == 0 ? null : "auto",
            parallel_tool_calls = request.Tools.Count == 0 ? (bool?)null : false,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = false
        };
    }

    private static async Task<ChatCompletionPayload?> ReadPayloadAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxResponseBytes)
            {
                throw InvalidResponse();
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;
        return await JsonSerializer.DeserializeAsync<ChatCompletionPayload>(buffer, JsonOptions, cancellationToken);
    }

    private static void ValidateRequest(AiModelGatewayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Messages.Count == 0 || request.Messages.Count > 100 ||
            request.Messages.Any(message =>
                message.Role is not ("system" or "user" or "assistant" or "tool") ||
                (string.IsNullOrWhiteSpace(message.Content) && message.ToolCalls.Count == 0) ||
                (message.Role == "tool") != !string.IsNullOrWhiteSpace(message.ToolCallId) ||
                message.ToolCalls.Any(call =>
                    string.IsNullOrWhiteSpace(call.Id) ||
                    string.IsNullOrWhiteSpace(call.Name) ||
                    !IsJsonObject(call.ArgumentsJson))))
        {
            throw new AiModelGatewayException(
                "request_invalid",
                ErrorCode.ValidationFailed,
                "The AI model gateway request is invalid.",
                false);
        }

        if (request.Tools.Count > 32 || request.Tools.Any(tool =>
                string.IsNullOrWhiteSpace(tool.Name) || tool.Name.Length > 64 ||
                tool.Name.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-') ||
                !IsJsonObject(tool.ParametersJson)))
        {
            throw new AiModelGatewayException(
                "request_invalid",
                ErrorCode.ValidationFailed,
                "The AI model tool catalog is invalid.",
                false);
        }
    }

    private static bool IsJsonObject(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static OpenAiCompatibleOptions ToOptions(AiProviderConnectionSettings provider)
    {
        return new OpenAiCompatibleOptions
        {
            Enabled = true,
            BaseUrl = provider.BaseUrl,
            ChatCompletionsPath = provider.ChatCompletionsPath,
            ApiKey = provider.ApiKey,
            Model = provider.ModelName,
            TimeoutSeconds = provider.TimeoutSeconds,
            AllowInsecureHttp = provider.AllowInsecureHttp,
            AllowPrivateNetwork = provider.AllowPrivateNetwork,
            AllowedHosts = provider.AllowedHosts.ToArray()
        };
    }

    private static AiModelGatewayException MapHttpError(HttpStatusCode statusCode)
    {
        var numericStatus = (int)statusCode;
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return new AiModelGatewayException(
                "provider_rate_limited",
                ErrorCode.TooManyRequests,
                "The AI provider rate limit was exceeded.",
                true,
                numericStatus);
        }

        if (numericStatus >= 500)
        {
            return new AiModelGatewayException(
                "provider_unavailable",
                ErrorCode.InternalServerError,
                "The AI provider is unavailable.",
                true,
                numericStatus);
        }

        return new AiModelGatewayException(
            "provider_request_rejected",
            ErrorCode.BadRequest,
            "The AI provider rejected the request.",
            false,
            numericStatus);
    }

    private static AiModelGatewayException InvalidResponse()
    {
        return new AiModelGatewayException(
            "provider_response_invalid",
            ErrorCode.InternalServerError,
            "The AI provider returned an invalid response.",
            false);
    }

    private sealed class ChatCompletionPayload
    {
        public string? Id { get; init; }

        public string Model { get; init; } = string.Empty;

        public IReadOnlyList<ChatChoice>? Choices { get; init; } = [];

        public ChatUsage? Usage { get; init; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; init; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; init; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; init; }

        [JsonPropertyName("tool_calls")]
        public IReadOnlyList<ToolCall>? ToolCalls { get; init; } = [];
    }

    private sealed class ToolCall
    {
        public string Id { get; init; } = string.Empty;

        public ToolFunction Function { get; init; } = new();
    }

    private sealed class ToolFunction
    {
        public string Name { get; init; } = string.Empty;

        public string Arguments { get; init; } = string.Empty;
    }

    private sealed class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; init; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; init; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; init; }
    }
}
