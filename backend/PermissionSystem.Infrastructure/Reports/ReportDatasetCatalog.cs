using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PermissionSystem.Application.Reports;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Infrastructure.Reports;

public sealed class ReportDatasetCatalog : IReportDatasetCatalog
{
    private static readonly Regex KeyRegex = new("^[a-z0-9][a-z0-9_-]{0,99}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex IdentifierRegex = new("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IReadOnlyList<ReportDatasetOptions> _datasets;

    public ReportDatasetCatalog(IOptions<ReportOptions> options)
    {
        _datasets = options.Value.Datasets ?? [];
        ValidateConfiguration(_datasets);
    }

    public IReadOnlyList<ReportDatasetResponse> GetAvailable()
    {
        return _datasets
            .Select(dataset => new ReportDatasetResponse { Key = dataset.Key, Name = dataset.Name })
            .ToList();
    }

    public ReportDatasetDefinition GetRequired(string datasetKey)
    {
        var dataset = Find(datasetKey);
        return new ReportDatasetDefinition
        {
            Key = dataset.Key,
            FilterParameterCodes = dataset.Filters
                .Select(filter => filter.ParamCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    public ReportDatasetOptions GetExecutionDefinition(string datasetKey)
    {
        return Find(datasetKey);
    }

    private ReportDatasetOptions Find(string datasetKey)
    {
        if (string.IsNullOrWhiteSpace(datasetKey))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "A report dataset is required.");
        }

        return _datasets.FirstOrDefault(dataset =>
                   string.Equals(dataset.Key, datasetKey.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new BusinessException(ErrorCode.ValidationFailed, "The report dataset is not registered.");
    }

    private static void ValidateConfiguration(IReadOnlyList<ReportDatasetOptions> datasets)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dataset in datasets)
        {
            if (!KeyRegex.IsMatch(dataset.Key) || !keys.Add(dataset.Key) || string.IsNullOrWhiteSpace(dataset.Name))
            {
                throw new InvalidOperationException("Report dataset keys and names must be unique and valid.");
            }

            var objectParts = dataset.ViewName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (objectParts.Length != 2 ||
                !string.Equals(objectParts[0], "reporting", StringComparison.OrdinalIgnoreCase) ||
                objectParts.Any(part => !IdentifierRegex.IsMatch(part)))
            {
                throw new InvalidOperationException($"Report dataset '{dataset.Key}' must reference a view in the reporting schema.");
            }

            var filterCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filter in dataset.Filters)
            {
                if (!IdentifierRegex.IsMatch(filter.ParamCode) || !filterCodes.Add(filter.ParamCode) || !IdentifierRegex.IsMatch(filter.ColumnName))
                {
                    throw new InvalidOperationException($"Report dataset '{dataset.Key}' contains an invalid filter definition.");
                }

                if (filter.Operator is not ("Equal" or "GreaterThanOrEqual" or "LessThanOrEqual"))
                {
                    throw new InvalidOperationException($"Report dataset '{dataset.Key}' contains an unsupported filter operator.");
                }
            }
        }
    }
}
