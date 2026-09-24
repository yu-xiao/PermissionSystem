using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Infrastructure.Options;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiEndpointValidatorTests
{
    [Theory]
    [InlineData("https://other.example.test/chat")]
    [InlineData("v1/chat?redirect=other")]
    [InlineData("v1/chat#fragment")]
    [InlineData("\\\\other.example.test/chat")]
    public void ValidateConfiguration_RejectsPathsThatAlterOriginOrAddQuery(string path)
    {
        Assert.Throws<AiModelGatewayException>(() => OpenAiCompatibleEndpointValidator.ValidateConfiguration(new OpenAiCompatibleOptions
        {
            Enabled = true, BaseUrl = "https://api.example.test/", ChatCompletionsPath = path,
            ApiKey = "synthetic-key", Model = "test", AllowedHosts = ["api.example.test"], TimeoutSeconds = 30
        }));
    }
}
