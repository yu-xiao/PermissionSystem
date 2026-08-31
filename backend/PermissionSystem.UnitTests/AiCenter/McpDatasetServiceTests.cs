using System.Text.Json;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Mcp;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class McpDatasetServiceTests
{
    private static readonly Guid TenantId = TestIds.TenantId;

    [Fact]
    public void BuiltInSchemaHashes_AreStable()
    {
        var hashes = McpBuiltInDatasetCatalog.Datasets.ToDictionary(
            dataset => dataset.DatasetCode,
            dataset => dataset.SchemaHash);

        Assert.Equal(
            "B9DCA44A8861B0327C5185CCE989DFC5B8234C57270BA1077AAEF73EA0FEE6C2",
            hashes[McpDatasetCodes.PlatformCapabilities]);
        Assert.Equal(
            "716DF9CB29D081721687E2420E981DB950CE82E7F8E262B2331FF7E489A4EDD0",
            hashes[McpDatasetCodes.DepartmentDirectory]);
    }

    [Fact]
    public void HandlerResolver_RejectsDuplicateHandlerCodes()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new McpDatasetQueryHandlerResolver(
            [
                new PlatformCapabilitiesMcpDatasetQueryHandler(),
                new PlatformCapabilitiesMcpDatasetQueryHandler()
            ]));
    }

    [Fact]
    public void HandlerResolver_RejectsUnknownHandlerCode()
    {
        var resolver = new McpDatasetQueryHandlerResolver(
            [new PlatformCapabilitiesMcpDatasetQueryHandler()]);

        var exception = Assert.Throws<BusinessException>(() => resolver.GetRequired("unknown"));

        Assert.Equal(ErrorCode.Conflict, exception.ErrorCode);
    }

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

    [Fact]
    public async Task QueryAsync_RejectsGrantApprovedForPreviousSchema()
    {
        var fixture = CreateFixture(["code"], approvedSchemaHash: new string('A', 64));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.QueryAsync(
            McpDatasetCodes.PlatformCapabilities,
            new McpDatasetQueryRequest { Fields = ["code"] }));

        Assert.Equal(ErrorCode.Forbidden, exception.ErrorCode);
        Assert.Equal("Denied", Assert.Single(fixture.Logs.Items).Status.ToString());
    }

    private static Fixture CreateFixture(
        IReadOnlyList<string> allowedFields,
        string? allowedScopes = null,
        string? approvedSchemaHash = null)
    {
        var template = McpBuiltInDatasetCatalog.Datasets.Single(candidate =>
            candidate.DatasetCode == McpDatasetCodes.PlatformCapabilities);
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
            SchemaHash = template.SchemaHash,
            PublicationStatus = McpDatasetPublicationStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow,
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
                ApprovedSchemaHash = approvedSchemaHash ?? dataset.SchemaHash,
                IsEnabled = true
            }),
            logRepository,
            new InMemoryAsyncQueryExecutor(),
            new TraceContextAccessor { TraceId = "trace-test" },
            new McpDatasetQueryHandlerResolver([new PlatformCapabilitiesMcpDatasetQueryHandler()]),
            new RecordingCircuitBreaker(),
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

    private sealed class RecordingCircuitBreaker : IAiCircuitBreaker
    {
        public Task<bool> AllowAsync(AiCircuitTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task RecordSuccessAsync(AiCircuitTarget target, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordFailureAsync(
            AiCircuitTarget target,
            string errorCode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
