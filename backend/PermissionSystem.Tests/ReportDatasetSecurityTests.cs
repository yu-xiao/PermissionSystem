using Microsoft.Extensions.Options;
using PermissionSystem.Infrastructure.Options;
using PermissionSystem.Infrastructure.Reports;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Tests;

public sealed class ReportDatasetSecurityTests
{
    [Fact]
    public void Catalog_ShouldExposeOnlyConfiguredDatasets()
    {
        var catalog = new ReportDatasetCatalog(Options.Create(new ReportOptions
        {
            Datasets =
            [
                new ReportDatasetOptions
                {
                    Key = "users",
                    Name = "Users",
                    ViewName = "reporting.Users",
                    Filters =
                    [
                        new ReportDatasetFilterOptions { ParamCode = "createdFrom", ColumnName = "CreatedAt", Operator = "GreaterThanOrEqual" }
                    ]
                }
            ]
        }));

        var dataset = Assert.Single(catalog.GetAvailable());

        Assert.Equal("users", dataset.Key);
        Assert.Contains("createdFrom", catalog.GetRequired("users").FilterParameterCodes);
    }

    [Fact]
    public void Catalog_ShouldRejectUnknownDataset()
    {
        var catalog = new ReportDatasetCatalog(Options.Create(new ReportOptions()));

        Assert.Throws<BusinessException>(() => catalog.GetRequired("users"));
    }

    [Theory]
    [InlineData("dbo.Users")]
    [InlineData("sys.objects")]
    [InlineData("reporting.Users.PasswordHash")]
    public void Catalog_ShouldRejectNonViewObjectNames(string viewName)
    {
        Assert.Throws<InvalidOperationException>(() => new ReportDatasetCatalog(Options.Create(new ReportOptions
        {
            Datasets = [new ReportDatasetOptions { Key = "users", Name = "Users", ViewName = viewName }]
        })));
    }
}
