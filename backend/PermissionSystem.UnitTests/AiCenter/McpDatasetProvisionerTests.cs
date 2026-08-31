using PermissionSystem.Application.Mcp;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class McpDatasetProvisionerTests
{
    private static readonly Guid TenantId = TestIds.TenantId;

    [Fact]
    public async Task ProvisionAsync_UpgradesLegacyGrantToInitialSchemaSnapshot()
    {
        var template = GetPlatformTemplate();
        var dataset = CreateDataset(string.Empty);
        var grant = CreateGrant(dataset.Id, string.Empty);
        var fixture = CreateFixture(dataset, grant);

        await fixture.Provisioner.EnsureTenantDatasetsAsync(TenantId);

        Assert.Equal(template.SchemaHash, dataset.SchemaHash);
        Assert.Equal(McpDatasetPublicationStatus.Published, dataset.PublicationStatus);
        Assert.NotNull(dataset.PublishedAt);
        Assert.Equal(template.SchemaHash, grant.ApprovedSchemaHash);
    }

    [Fact]
    public async Task ProvisionAsync_SchemaChangeInvalidatesGrantAndRemovesObsoleteField()
    {
        var previousHash = new string('A', 64);
        var dataset = CreateDataset(previousHash);
        var grant = CreateGrant(dataset.Id, previousHash);
        var obsoleteField = new McpDatasetField
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DatasetId = dataset.Id,
            FieldCode = "obsolete",
            DisplayName = "Obsolete",
            DataType = "string",
            DataClassification = "Public",
            IsFilterable = false,
            IsDefault = false
        };
        var fixture = CreateFixture(dataset, grant, obsoleteField);

        await fixture.Provisioner.EnsureTenantDatasetsAsync(TenantId);

        Assert.Equal(GetPlatformTemplate().SchemaHash, dataset.SchemaHash);
        Assert.Equal(previousHash, grant.ApprovedSchemaHash);
        Assert.True(obsoleteField.IsDeleted);
    }

    private static Fixture CreateFixture(
        McpDatasetDefinition dataset,
        McpClientDatasetGrant grant,
        params McpDatasetField[] fields)
    {
        var datasets = new InMemoryRepository<McpDatasetDefinition>(dataset);
        var datasetFields = new InMemoryRepository<McpDatasetField>(fields);
        var grants = new InMemoryRepository<McpClientDatasetGrant>(grant);
        var provisioner = new McpDatasetProvisioner(
            datasets,
            datasetFields,
            grants,
            new InMemoryAsyncQueryExecutor(),
            new TestUnitOfWork());
        return new Fixture(provisioner);
    }

    private static McpDatasetDefinition CreateDataset(string schemaHash) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        DatasetCode = McpDatasetCodes.PlatformCapabilities,
        DatasetName = "Legacy platform capabilities",
        Version = "1.0",
        DataClassification = "Public",
        HandlerCode = McpDatasetCodes.PlatformCapabilities,
        MaxRows = 20,
        SchemaHash = schemaHash,
        PublicationStatus = McpDatasetPublicationStatus.Published,
        IsEnabled = true
    };

    private static McpClientDatasetGrant CreateGrant(Guid datasetId, string schemaHash) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        ClientBindingId = Guid.NewGuid(),
        DatasetId = datasetId,
        AllowedFieldsJson = "[\"code\"]",
        ApprovedSchemaHash = schemaHash,
        IsEnabled = true
    };

    private static McpDatasetTemplate GetPlatformTemplate() =>
        McpBuiltInDatasetCatalog.Datasets.Single(dataset =>
            dataset.DatasetCode == McpDatasetCodes.PlatformCapabilities);

    private sealed record Fixture(McpDatasetProvisioner Provisioner);
}
