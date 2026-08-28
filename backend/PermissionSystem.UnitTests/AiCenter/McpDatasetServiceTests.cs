using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class McpDatasetServiceTests
{
    private static readonly Guid TenantId = TestIds.TenantId;

    [Fact]
    public async Task QueryAsync_RejectsFieldOutsideClientGrantAndAuditsDenial()
    {
        var fixture = CreateFixture(["code"]);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.QueryAsync(
            McpDatasetCodes.PlatformCapabilities,
            new McpDatasetQueryRequest { Fields = ["name"] }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        var log = Assert.Single(fixture.Logs.Items);
        Assert.Equal("query_dataset", log.ToolName);
        Assert.Equal("Denied", log.Status.ToString());
        Assert.Equal(64, log.InputDigest.Length);
    }

    [Fact]
    public async Task QueryAsync_ProjectsAuthorizedFieldsAndReportsTruncation()
    {
        var fixture = CreateFixture(["code"]);

        var result = await fixture.Service.QueryAsync(
            McpDatasetCodes.PlatformCapabilities,
            new McpDatasetQueryRequest { Fields = ["code"], Limit = 1 });

        Assert.Equal(["code"], result.Fields);
        Assert.Single(result.Rows);
        Assert.True(result.Rows[0].ContainsKey("code"));
        Assert.True(result.IsTruncated);
        Assert.Equal("trace-test", result.TraceId);
        Assert.Equal(1, Assert.Single(fixture.Logs.Items).RowCount);
    }

    [Fact]
    public async Task QueryAsync_RejectsFilterWithInvalidType()
    {
        var fixture = CreateFixture(["code"]);
        using var document = JsonDocument.Parse("true");

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.QueryAsync(
            McpDatasetCodes.PlatformCapabilities,
            new McpDatasetQueryRequest
            {
                Filters = new Dictionary<string, JsonElement> { ["code"] = document.RootElement.Clone() }
            }));

        Assert.Equal(ErrorCode.ValidationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task ListAsync_RejectsClientWithoutRequiredScope()
    {
        var fixture = CreateFixture(["code"], allowedScopes: McpToolScopes.DatasetQuery);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.ListAsync());

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal("Denied", Assert.Single(fixture.Logs.Items).Status.ToString());
    }

    private static Fixture CreateFixture(IReadOnlyList<string> allowedFields, string? allowedScopes = null)
    {
        var dataset = new McpDatasetDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DatasetCode = McpDatasetCodes.PlatformCapabilities,
            DatasetName = "Platform capabilities",
            Version = "1.0",
            HandlerCode = McpDatasetCodes.PlatformCapabilities,
            DataClassification = "Public",
            MaxRows = 20,
            IsEnabled = true
        };
        var fields = new[]
        {
            CreateField(dataset.Id, "code"),
            CreateField(dataset.Id, "name")
        };
        var bindingId = Guid.NewGuid();
        var callerContext = new McpCallerContext();
        callerContext.SetServiceClient(new McpServiceClientRecord
        {
            TenantId = TenantId,
            ClientBindingId = bindingId,
            ApiClientId = Guid.NewGuid(),
            OAuthClientId = "mcp-test",
            ClientCode = "test",
            IsEnabled = true,
            AllowedScopes = allowedScopes ?? string.Join(',', McpToolScopes.All),
            AllowedIpList = "*",
            RateLimitPerMinute = 60
        }, "127.0.0.1");

        var logRepository = new InMemoryRepository<McpInvocationLog>();
        var service = new McpDatasetService(
            callerContext,
            new TestCurrentUserService(),
            new InMemoryRepository<McpDatasetDefinition>(dataset),
            new InMemoryRepository<McpDatasetField>(fields),
            new InMemoryRepository<McpClientDatasetGrant>(new McpClientDatasetGrant
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ClientBindingId = bindingId,
                DatasetId = dataset.Id,
                AllowedFieldsJson = JsonSerializer.Serialize(allowedFields),
                IsEnabled = true
            }),
            logRepository,
            new InMemoryRepository<Department>(),
            new InMemoryAsyncQueryExecutor(),
            new TraceContextAccessor { TraceId = "trace-test" },
            new TestUnitOfWork());
        return new Fixture(service, logRepository);
    }

    private static McpDatasetField CreateField(Guid datasetId, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        DatasetId = datasetId,
        FieldCode = code,
        DisplayName = code,
        DataType = "string",
        DataClassification = "Public",
        IsFilterable = true,
        IsDefault = true
    };

    private sealed record Fixture(McpDatasetService Service, InMemoryRepository<McpInvocationLog> Logs);
}
