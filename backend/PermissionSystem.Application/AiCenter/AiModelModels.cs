using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Application.AiCenter;

public sealed record AiChatMessage(string Role, string Content);

public sealed class AiChatCompletionRequest
{
    public IReadOnlyList<AiChatMessage> Messages { get; init; } = [];

    public decimal? Temperature { get; init; }

    public int? MaxTokens { get; init; }
}

public sealed class AiChatCompletionResponse
{
    public string Content { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? TotalTokens { get; init; }
}

public interface IAiModelClient
{
    Task<AiChatCompletionResponse> CompleteAsync(
        AiChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AiModelToolDefinition
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ParametersJson { get; init; } = string.Empty;
}

public sealed class AiModelToolCall
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ArgumentsJson { get; init; } = string.Empty;
}

public sealed class AiModelGatewayMessage
{
    public string Role { get; init; } = string.Empty;

    public string? Content { get; init; }

    public string? ToolCallId { get; init; }

    public IReadOnlyList<AiModelToolCall> ToolCalls { get; init; } = [];
}

public sealed class AiModelGatewayRequest
{
    public IReadOnlyList<AiModelGatewayMessage> Messages { get; init; } = [];

    public IReadOnlyList<AiModelToolDefinition> Tools { get; init; } = [];

    public decimal? Temperature { get; init; }

    public int? MaxTokens { get; init; }
}

public sealed class AiModelGatewayResponse
{
    public string? Content { get; init; }

    public IReadOnlyList<AiModelToolCall> ToolCalls { get; init; } = [];

    public string Model { get; init; } = string.Empty;

    public string? ProviderRequestId { get; init; }

    public string? FinishReason { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? TotalTokens { get; init; }
}

public interface IAiModelGateway
{
    Task<AiModelGatewayResponse> CompleteAsync(
        AiProviderConnectionSettings provider,
        AiModelGatewayRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AiModelGatewayException : Exception
{
    public AiModelGatewayException(
        string errorType,
        ErrorCode errorCode,
        string message,
        bool isTransient,
        int? providerStatusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorType = errorType;
        ErrorCode = errorCode;
        IsTransient = isTransient;
        ProviderStatusCode = providerStatusCode;
    }

    public string ErrorType { get; }

    public ErrorCode ErrorCode { get; }

    public bool IsTransient { get; }

    public int? ProviderStatusCode { get; }
}
