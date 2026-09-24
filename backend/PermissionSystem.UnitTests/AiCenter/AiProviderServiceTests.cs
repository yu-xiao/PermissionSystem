using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiProviderServiceTests
{
    [Fact]
    public async Task CreateAsync_EncryptsApiKeyAndReturnsOnlyMask()
    {
        var providers = new InMemoryRepository<AiProviderConfig>();
        var tester = new TestConnectionTester();
        var service = CreateService(providers, tester: tester);

        var response = await service.CreateAsync(ValidCreateRequest());

        var provider = Assert.Single(providers.Items);
        Assert.Equal("protected:test-api-key", provider.ApiKeyEncrypted);
        Assert.Equal("********", response.ApiKey);
        Assert.True(response.HasApiKey);
        Assert.Equal("test-api-key", tester.LastSettings?.ApiKey);
        Assert.DoesNotContain(
            "test-api-key",
            System.Text.Json.JsonSerializer.Serialize(response),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("********")]
    public async Task UpdateAsync_WithUnchangedEndpointPreservesEncryptedSecretAndCompliance(string? apiKey)
    {
        var provider = ExistingProvider();
        var confirmedAt = DateTimeOffset.UtcNow;
        provider.ComplianceConfirmedAt = confirmedAt;
        var providers = new InMemoryRepository<AiProviderConfig>(provider);
        var service = CreateService(providers);

        await service.UpdateAsync(provider.Id, new UpdateAiProviderRequest
        {
            ProviderName = "Updated provider",
            BaseUrl = $" {provider.BaseUrl} ",
            ChatCompletionsPath = $" {provider.ChatCompletionsPath} ",
            ApiKey = apiKey,
            ModelName = provider.ModelName,
            AllowedHosts = ["api.example.test"]
        });

        Assert.Equal("protected:test-api-key", provider.ApiKeyEncrypted);
        Assert.Equal("Updated provider", provider.ProviderName);
        Assert.Equal(confirmedAt, provider.ComplianceConfirmedAt);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "  ")]
    [InlineData(false, " ******** ")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "  ")]
    [InlineData(true, " ******** ")]
    public async Task UpdateAsync_WithChangedEndpointAndNoExplicitKeyIsRejected(bool changePath, string? apiKey)
    {
        var provider = ExistingProvider();
        var confirmedAt = DateTimeOffset.UtcNow;
        provider.ComplianceConfirmedAt = confirmedAt;
        var tester = new TestConnectionTester();
        var unitOfWork = new TestUnitOfWork();
        var service = CreateService(new InMemoryRepository<AiProviderConfig>(provider),
            tester: tester, unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(provider.Id,
            EndpointUpdateRequest(provider, changePath, apiKey)));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
        Assert.Equal("https://api.example.test", provider.BaseUrl);
        Assert.Equal("v1/chat/completions", provider.ChatCompletionsPath);
        Assert.Equal("protected:test-api-key", provider.ApiKeyEncrypted);
        Assert.Equal(confirmedAt, provider.ComplianceConfirmedAt);
        Assert.Null(tester.LastSettings);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAsync_WithChangedEndpointRequiresComplianceBeforeConnectionTest(bool changePath)
    {
        var provider = ExistingProvider();
        provider.ComplianceConfirmedAt = DateTimeOffset.UtcNow;
        var tester = new TestConnectionTester();
        var service = CreateService(new InMemoryRepository<AiProviderConfig>(provider),
            tester: tester, configuration: new TestAiCenterConfiguration { Enabled = true });

        var request = EndpointUpdateRequest(provider, changePath, " replacement-test-key ");
        var response = await service.UpdateAsync(provider.Id, request);

        Assert.Equal(request.BaseUrl, provider.BaseUrl);
        Assert.Equal(request.ChatCompletionsPath, provider.ChatCompletionsPath);
        Assert.Equal("protected:replacement-test-key", provider.ApiKeyEncrypted);
        Assert.Equal("********", response.ApiKey);
        Assert.Null(provider.ComplianceConfirmedAt);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.TestAsync(provider.Id));
        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal(0, tester.TestCount);

        await service.SetComplianceAsync(provider.Id, new SetAiProviderComplianceRequest { IsConfirmed = true });
        await service.TestAsync(provider.Id);
        Assert.Equal(1, tester.TestCount);
        Assert.Equal("replacement-test-key", tester.LastSettings?.ApiKey);
        Assert.Equal(request.BaseUrl, tester.LastSettings?.BaseUrl);
    }

    private static UpdateAiProviderRequest EndpointUpdateRequest(AiProviderConfig provider, bool changePath, string? apiKey) => new()
    {
        ProviderName = provider.ProviderName,
        BaseUrl = changePath ? provider.BaseUrl : "https://replacement.example.test",
        ChatCompletionsPath = changePath ? "v2/chat/completions" : provider.ChatCompletionsPath,
        ApiKey = apiKey,
        ModelName = provider.ModelName,
        AllowedHosts = [changePath ? "api.example.test" : "replacement.example.test"]
    };

    [Fact]
    public async Task SetDefaultAsync_ClearsPreviousDefaultWithinTransaction()
    {
        var previous = ExistingProvider();
        previous.IsDefault = true;
        var target = ExistingProvider("secondary");
        var providers = new InMemoryRepository<AiProviderConfig>(previous, target);
        var unitOfWork = new TestUnitOfWork();
        var service = CreateService(providers, unitOfWork: unitOfWork);

        await service.SetDefaultAsync(target.Id);

        Assert.False(previous.IsDefault);
        Assert.True(target.IsDefault);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(2, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task TestAsync_WhenKillSwitchDisabledDoesNotCallProvider()
    {
        var provider = ExistingProvider();
        var tester = new TestConnectionTester();
        var service = CreateService(
            new InMemoryRepository<AiProviderConfig>(provider),
            tester: tester,
            configuration: new TestAiCenterConfiguration { Enabled = false });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.TestAsync(provider.Id));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal(0, tester.TestCount);
    }

    [Fact]
    public async Task TestAsync_WhenTenantIsNotAllowlistedDoesNotCallProvider()
    {
        var provider = ExistingProvider();
        var tester = new TestConnectionTester();
        var service = CreateService(
            new InMemoryRepository<AiProviderConfig>(provider),
            tester: tester,
            configuration: new TestAiCenterConfiguration
            {
                Enabled = true,
                AllowedTenantIds = [Guid.NewGuid()]
            });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.TestAsync(provider.Id));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal(0, tester.TestCount);
    }

    [Fact]
    public async Task DeleteAsync_WhenProviderHasRunIsRejected()
    {
        var provider = ExistingProvider();
        var run = new AiRun
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ProviderConfigId = provider.Id
        };
        var providers = new InMemoryRepository<AiProviderConfig>(provider);
        var service = CreateService(providers, runs: new InMemoryRepository<AiRun>(run));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(provider.Id));

        Assert.Equal(ErrorCode.Conflict, exception.ErrorCode);
        Assert.False(provider.IsDeleted);
    }

    [Fact]
    public async Task CreateAsync_WithDisabledDefaultProviderIsRejected()
    {
        var service = CreateService(new InMemoryRepository<AiProviderConfig>());
        var request = ValidCreateRequest(isDefault: true, isEnabled: false);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(request));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WithNullAllowedHostsReturnsValidationError()
    {
        var service = CreateService(new InMemoryRepository<AiProviderConfig>());
        var request = new CreateAiProviderRequest
        {
            ProviderCode = "primary",
            ProviderName = "Primary provider",
            BaseUrl = "https://api.example.test",
            ApiKey = "test-api-key",
            ModelName = "test-model",
            AllowedHosts = null!
        };

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(request));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task TestAsync_WhenComplianceIsNotConfirmedDoesNotCallProvider()
    {
        var provider = ExistingProvider();
        var tester = new TestConnectionTester();
        var service = CreateService(
            new InMemoryRepository<AiProviderConfig>(provider),
            tester: tester,
            configuration: new TestAiCenterConfiguration { Enabled = true });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.TestAsync(provider.Id));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal(0, tester.TestCount);
    }

    [Fact]
    public async Task SetComplianceAsync_RecordsExplicitConfirmation()
    {
        var provider = ExistingProvider();
        var service = CreateService(new InMemoryRepository<AiProviderConfig>(provider));

        await service.SetComplianceAsync(provider.Id, new SetAiProviderComplianceRequest { IsConfirmed = true });

        Assert.NotNull(provider.ComplianceConfirmedAt);
    }

    private static AiProviderService CreateService(
        InMemoryRepository<AiProviderConfig> providers,
        InMemoryRepository<AiRun>? runs = null,
        TestConnectionTester? tester = null,
        TestUnitOfWork? unitOfWork = null,
        IAiCenterConfiguration? configuration = null)
    {
        return new AiProviderService(
            providers,
            runs ?? new InMemoryRepository<AiRun>(),
            new InMemoryAsyncQueryExecutor(),
            new TestConfigValueProtector(),
            new TestTenantWriteResolver(),
            tester ?? new TestConnectionTester(),
            unitOfWork ?? new TestUnitOfWork(),
            configuration);
    }

    private static CreateAiProviderRequest ValidCreateRequest(bool isDefault = false, bool isEnabled = true)
    {
        return new CreateAiProviderRequest
        {
            ProviderCode = "primary",
            ProviderName = "Primary provider",
            BaseUrl = "https://api.example.test",
            ChatCompletionsPath = "v1/chat/completions",
            ApiKey = "test-api-key",
            ModelName = "test-model",
            IsDefault = isDefault,
            IsEnabled = isEnabled,
            AllowedHosts = ["api.example.test"]
        };
    }

    private static AiProviderConfig ExistingProvider(string code = "primary")
    {
        return new AiProviderConfig
        {
            Id = Guid.NewGuid(),
            TenantId = TestIds.TenantId,
            ProviderCode = code,
            ProviderName = code,
            ProviderType = AiProviderType.OpenAiCompatible,
            BaseUrl = "https://api.example.test",
            ChatCompletionsPath = "v1/chat/completions",
            ApiKeyEncrypted = "protected:test-api-key",
            ModelName = "test-model",
            IsEnabled = true,
            AllowedHostsJson = "[\"api.example.test\"]"
        };
    }

    private sealed class TestConnectionTester : IAiProviderConnectionTester
    {
        public AiProviderConnectionSettings? LastSettings { get; private set; }

        public int TestCount { get; private set; }

        public void Validate(AiProviderConnectionSettings settings)
        {
            LastSettings = settings;
        }

        public Task<AiProviderConnectionTestResult> TestAsync(
            AiProviderConnectionSettings settings,
            CancellationToken cancellationToken = default)
        {
            LastSettings = settings;
            TestCount++;
            return Task.FromResult(new AiProviderConnectionTestResult
            {
                Succeeded = true,
                Message = "Succeeded",
                ModelName = settings.ModelName
            });
        }
    }

    private sealed class TestAiCenterConfiguration : IAiCenterConfiguration
    {
        public bool Enabled { get; init; }

        public IReadOnlyCollection<Guid> AllowedTenantIds { get; init; } = [TestIds.TenantId];

        public int ConversationRetentionDays => 30;

        public int AuditRetentionDays => 180;
    }
}
