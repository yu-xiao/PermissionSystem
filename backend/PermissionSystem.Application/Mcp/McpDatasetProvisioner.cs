using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;

namespace PermissionSystem.Application.Mcp;

public interface IMcpDatasetProvisioner
{
    Task EnsureTenantDatasetsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class McpDatasetProvisioner : IMcpDatasetProvisioner
{
    private readonly IRepository<McpDatasetDefinition> _datasetRepository;
    private readonly IRepository<McpDatasetField> _fieldRepository;
    private readonly IRepository<McpClientDatasetGrant> _grantRepository;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly IUnitOfWork _unitOfWork;

    public McpDatasetProvisioner(
        IRepository<McpDatasetDefinition> datasetRepository,
        IRepository<McpDatasetField> fieldRepository,
        IRepository<McpClientDatasetGrant> grantRepository,
        IAsyncQueryExecutor queryExecutor,
        IUnitOfWork unitOfWork)
    {
        _datasetRepository = datasetRepository;
        _fieldRepository = fieldRepository;
        _grantRepository = grantRepository;
        _queryExecutor = queryExecutor;
        _unitOfWork = unitOfWork;
    }

    public async Task EnsureTenantDatasetsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var template in McpBuiltInDatasetCatalog.Datasets)
        {
            var isNewDataset = false;
            var dataset = await _queryExecutor.FirstOrDefaultAsync(
                _datasetRepository.QueryForTenant(tenantId).Where(entity =>
                    entity.DatasetCode == template.DatasetCode && entity.Version == template.Version),
                cancellationToken);
            if (dataset is null)
            {
                isNewDataset = true;
                dataset = new McpDatasetDefinition
                {
                    TenantId = tenantId,
                    DatasetCode = template.DatasetCode,
                    Version = template.Version
                };
                await _datasetRepository.AddAsync(dataset, cancellationToken);
            }

            var previousSchemaHash = dataset.SchemaHash;
            dataset.DatasetName = template.DatasetName;
            dataset.Description = template.Description;
            dataset.DataClassification = template.DataClassification;
            dataset.HandlerCode = template.HandlerCode;
            dataset.MaxRows = template.MaxRows;
            dataset.SchemaHash = template.SchemaHash;
            dataset.PublicationStatus = McpDatasetPublicationStatus.Published;
            if (isNewDataset || !string.Equals(previousSchemaHash, dataset.SchemaHash, StringComparison.Ordinal))
            {
                dataset.PublishedAt = DateTimeOffset.UtcNow;
            }
            dataset.IsEnabled = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var existingFields = await _queryExecutor.ToListAsync(
                _fieldRepository.QueryForTenant(tenantId)
                    .Where(entity => entity.DatasetId == dataset.Id),
                cancellationToken);
            foreach (var fieldTemplate in template.Fields)
            {
                var field = existingFields.FirstOrDefault(entity =>
                    string.Equals(entity.FieldCode, fieldTemplate.FieldCode, StringComparison.Ordinal));
                if (field is null)
                {
                    field = new McpDatasetField
                    {
                        TenantId = tenantId,
                        DatasetId = dataset.Id,
                        FieldCode = fieldTemplate.FieldCode
                    };
                    await _fieldRepository.AddAsync(field, cancellationToken);
                }

                field.DisplayName = fieldTemplate.DisplayName;
                field.DataType = fieldTemplate.DataType;
                field.DataClassification = fieldTemplate.DataClassification;
                field.IsFilterable = fieldTemplate.IsFilterable;
                field.IsDefault = fieldTemplate.IsDefault;
            }

            var templateFieldCodes = template.Fields
                .Select(field => field.FieldCode)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var obsoleteField in existingFields.Where(field =>
                         !templateFieldCodes.Contains(field.FieldCode)))
            {
                _fieldRepository.Remove(obsoleteField);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (!isNewDataset && string.IsNullOrWhiteSpace(previousSchemaHash))
            {
                var legacyGrants = await _queryExecutor.ToListAsync(
                    _grantRepository.QueryForTenant(tenantId).Where(entity =>
                        entity.DatasetId == dataset.Id && entity.ApprovedSchemaHash == string.Empty),
                    cancellationToken);
                foreach (var grant in legacyGrants)
                {
                    grant.ApprovedSchemaHash = dataset.SchemaHash;
                }

                if (legacyGrants.Count > 0)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}
