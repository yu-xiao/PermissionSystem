using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class OpenAiCompatibleModelClient : IAiModelClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiCompatibleOptions _options;

    public OpenAiCompatibleModelClient(
        HttpClient httpClient,
        IOptions<OpenAiCompatibleOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AiChatCompletionResponse> CompleteAsync(
        AiChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var endpoint = OpenAiCompatibleEndpointValidator.ValidateConfiguration(_options);
        await OpenAiCompatibleEndpointValidator.ValidateResolvedAddressesAsync(
            endpoint,
            _options.AllowPrivateNetwork,
            cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        message.Content = JsonContent.Create(new
        {
            model = _options.Model,
            messages = request.Messages.Select(item => new { role = item.Role, content = item.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = false
        }, options: JsonOptions);

        try
        {
            using var response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw MapHttpError(response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<ChatCompletionPayload>(
                JsonOptions,
                timeoutSource.Token);
            var content = payload?.Choices.FirstOrDefault()?.Message.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiModelGatewayException(
                    "provider_response_invalid",
                    ErrorCode.InternalServerError,
                    "The AI provider returned an invalid response.",
                    false);
            }

            return new AiChatCompletionResponse
            {
                Content = content,
                Model = string.IsNullOrWhiteSpace(payload!.Model) ? _options.Model : payload.Model,
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

    private static void ValidateRequest(AiChatCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Messages.Count == 0 || request.Messages.Count > 100 ||
            request.Messages.Any(message =>
                message.Role is not ("system" or "user" or "assistant") ||
                string.IsNullOrWhiteSpace(message.Content)))
        {
            throw new AiModelGatewayException(
                "request_invalid",
                ErrorCode.ValidationFailed,
                "The AI chat request is invalid.",
                false);
        }
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

    private sealed class ChatCompletionPayload
    {
        public string Model { get; init; } = string.Empty;

        public IReadOnlyList<ChatChoice> Choices { get; init; } = [];

        public ChatUsage? Usage { get; init; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage Message { get; init; } = new();
    }

    private sealed class ChatMessage
    {
        public string Content { get; init; } = string.Empty;
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
