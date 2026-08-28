using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiProviderConnectionTester : IAiProviderConnectionTester
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AiProviderConnectionTester(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void Validate(AiProviderConnectionSettings settings)
    {
        _ = OpenAiCompatibleEndpointValidator.ValidateConfiguration(ToOptions(settings));
    }

    public async Task<AiProviderConnectionTestResult> TestAsync(
        AiProviderConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var client = new OpenAiCompatibleModelClient(
            _httpClientFactory.CreateClient("AiProviderConnectionTest"),
            Microsoft.Extensions.Options.Options.Create(ToOptions(settings)));
        var response = await client.CompleteAsync(
            new AiChatCompletionRequest
            {
                Messages =
                [
                    new AiChatMessage("system", "You are a connectivity probe. Do not use tools or return sensitive data."),
                    new AiChatMessage("user", "Reply with OK.")
                ],
                Temperature = 0,
                MaxTokens = 8
            },
            cancellationToken);

        return new AiProviderConnectionTestResult
        {
            Succeeded = true,
            Message = "AI provider connection succeeded.",
            ModelName = response.Model
        };
    }

    private static OpenAiCompatibleOptions ToOptions(AiProviderConnectionSettings settings)
    {
        return new OpenAiCompatibleOptions
        {
            Enabled = true,
            BaseUrl = settings.BaseUrl,
            ChatCompletionsPath = settings.ChatCompletionsPath,
            ApiKey = settings.ApiKey,
            Model = settings.ModelName,
            TimeoutSeconds = settings.TimeoutSeconds,
            AllowInsecureHttp = settings.AllowInsecureHttp,
            AllowPrivateNetwork = settings.AllowPrivateNetwork,
            AllowedHosts = settings.AllowedHosts.ToArray()
        };
    }
}
