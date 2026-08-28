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
