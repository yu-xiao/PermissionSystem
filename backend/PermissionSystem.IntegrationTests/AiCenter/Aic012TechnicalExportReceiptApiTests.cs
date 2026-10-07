using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic012TechnicalExportReceiptApiTests
{
    [Theory]
    [InlineData(false, false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, false, HttpStatusCode.Forbidden)]
    [InlineData(true, true, false, HttpStatusCode.Forbidden)]
    [InlineData(true, false, true, HttpStatusCode.Forbidden)]
    [InlineData(true, true, true, HttpStatusCode.OK)]
    public async Task ReceiptRoutes_RequireBothPermissionsAndReturnPrivateSafeDto(bool authenticated, bool view, bool export, HttpStatusCode expected)
    {
        var state = new Aic012OperationsApiTests.ContractService();
        using var server = await Aic012OperationsApiTests.Server(state); using var client = server.GetTestClient();
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        if (view) client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.OperationsViewPermission);
        if (export) client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.OperationsExportPermission);
        foreach (var path in new[] { AiTechnicalExportReceiptContract.Route, $"{AiTechnicalExportReceiptContract.Route}/{Guid.NewGuid()}" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(expected, response.StatusCode);
            if (expected != HttpStatusCode.OK) continue;
            Assert.True(response.Headers.CacheControl?.NoStore); Assert.Null(response.Content.Headers.ContentDisposition);
            var body = await response.Content.ReadAsStringAsync(); using var json = JsonDocument.Parse(body);
            Assert.Equal("CurrentCaller", json.RootElement.GetProperty("data").GetProperty("scope").GetString());
            Assert.DoesNotContain("requestBody", body); Assert.DoesNotContain("userId", body);
        }
        Assert.Equal(expected == HttpStatusCode.OK ? 2 : 0, state.ReceiptCalls); Assert.Equal(0, state.ExportCalls);
    }

    [Fact]
    public async Task ReceiptRoutes_BindOnlyIndependentWindowAndPagination()
    {
        var state = new Aic012OperationsApiTests.ContractService();
        using var server = await Aic012OperationsApiTests.Server(state); using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", new[] { AiCenterConstants.OperationsViewPermission, AiCenterConstants.OperationsExportPermission });
        const string from = "2026-10-01T00:00:00+08:00";
        var query = $"from={Uri.EscapeDataString(from)}&to=2026-10-02T00:00:00Z&pageIndex=2&pageSize=10&tenantId={Guid.NewGuid()}&userId={Guid.NewGuid()}";
        using var response = await client.GetAsync($"{AiTechnicalExportReceiptContract.Route}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.IsType<AiTechnicalExportReceiptQuery>(state.ReceiptRequest);
        Assert.Equal(DateTimeOffset.Parse(from), request.From); Assert.Equal(2, request.PageIndex); Assert.Equal(10, request.PageSize);
        Assert.Equal(new[] { "PageIndex", "PageSize", "From", "To" }, typeof(AiTechnicalExportReceiptQuery).GetProperties().Select(p => p.Name));
        var id = Guid.NewGuid();
        using var detail = await client.GetAsync($"{AiTechnicalExportReceiptContract.Route}/{id}?{query}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode); Assert.Equal(id, state.ReceiptExportId);
        using var invalid = await client.GetAsync($"{AiTechnicalExportReceiptContract.Route}?from=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.Equal(2, state.ReceiptCalls);
    }
}
