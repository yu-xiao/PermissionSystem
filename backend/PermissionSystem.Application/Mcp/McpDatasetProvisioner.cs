using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
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
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly IUnitOfWork _unitOfWork;

    public McpDatasetProvisioner(
        IRepository<McpDatasetDefinition> datasetRepository,
        IRepository<McpDatasetField> fieldRepository,
        IAsyncQueryExecutor queryExecutor,
        IUnitOfWork unitOfWork)
    {
        _datasetRepository = datasetRepository;
        _fieldRepository = fieldRepository;
        _queryExecutor = queryExecutor;
        _unitOfWork = unitOfWork;
    }

    public async Task EnsureTenantDatasetsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var template in McpBuiltInDatasetCatalog.Datasets)
        {
            var dataset = await _queryExecutor.FirstOrDefaultAsync(
                _datasetRepository.QueryForTenant(tenantId).Where(entity =>
                    entity.DatasetCode == template.DatasetCode && entity.Version == template.Version),
                cancellationToken);
            if (dataset is null)
            {
                dataset = new McpDatasetDefinition
                {
                    TenantId = tenantId,
                    DatasetCode = template.DatasetCode,
                    Version = template.Version
                };
                await _datasetRepository.AddAsync(dataset, cancellationToken);
            }

            dataset.DatasetName = template.DatasetName;
            dataset.Description = template.Description;
            dataset.DataClassification = template.DataClassification;
            dataset.HandlerCode = template.HandlerCode;
            dataset.MaxRows = template.MaxRows;
            dataset.IsEnabled = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var fieldTemplate in template.Fields)
            {
                var field = await _queryExecutor.FirstOrDefaultAsync(
                    _fieldRepository.QueryForTenant(tenantId).Where(entity =>
                        entity.DatasetId == dataset.Id && entity.FieldCode == fieldTemplate.FieldCode),
                    cancellationToken);
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
