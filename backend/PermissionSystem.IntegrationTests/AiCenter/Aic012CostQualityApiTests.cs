using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic012CostQualityApiTests
{
    [Theory]
    [InlineData(false, "ai:operations:view", HttpStatusCode.Unauthorized)]
    [InlineData(true, "ai:operations:export", HttpStatusCode.Forbidden)]
    [InlineData(true, "ai:operations:view", HttpStatusCode.OK)]
    public async Task Query_RequiresViewWithoutExportAndReturnsNoStoreSafeDto(bool authenticated, string permission, HttpStatusCode expected)
    {
        var state = new Aic012OperationsApiTests.ContractService(); using var host = await Aic012OperationsApiTests.Server(state); using var client = host.GetTestClient();
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
        using var response = await client.GetAsync(AiCostQualityContract.Route);
        Assert.Equal(expected, response.StatusCode); Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, state.CostQualityCalls);
        Assert.Equal(0, state.ExportCalls); Assert.Equal(0, state.ReceiptCalls);
        if (expected != HttpStatusCode.OK) return;
        Assert.True(response.Headers.CacheControl?.NoStore); var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body); var data = json.RootElement.GetProperty("data");
        Assert.Equal(AiCostQualityContract.Scope, data.GetProperty("scope").GetString());
        Assert.Equal(50000, data.GetProperty("limits").GetProperty("maxUsages").GetInt32());
        foreach (var excluded in new[] { "inputTokenPricePerMillion", "providerRequestId", "usageId", "runId", "modelName", "userId" }) Assert.DoesNotContain(excluded, body);
    }
    [Fact]
    public async Task Query_BindsOnlyUsageWindowAndCurrencyPagination()
    {
        var state = new Aic012OperationsApiTests.ContractService(); using var host = await Aic012OperationsApiTests.Server(state); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true"); client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.OperationsViewPermission);
        const string from = "2026-10-01T00:00:00+08:00";
        using var response = await client.GetAsync($"{AiCostQualityContract.Route}?from={Uri.EscapeDataString(from)}&to=2026-10-02T00:00:00Z&pageIndex=2&pageSize=10&tenantId={Guid.NewGuid()}&providerId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(DateTimeOffset.Parse(from), state.CostQualityRequest!.From);
        Assert.Equal(2, state.CostQualityRequest.PageIndex); Assert.Equal(10, state.CostQualityRequest.PageSize);
        Assert.Equal(new[] { "From", "To", "PageIndex", "PageSize" }, typeof(AiCostQualityQuery).GetProperties().Select(p => p.Name));
    }
    [Theory]
    [InlineData("from=invalid")]
    [InlineData("pageSize=invalid")]
    public async Task Query_RejectsMalformedDtoBeforeApplication(string query)
    {
        var state = new Aic012OperationsApiTests.ContractService(); using var host = await Aic012OperationsApiTests.Server(state); using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true"); client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.OperationsViewPermission);
        using var response = await client.GetAsync($"{AiCostQualityContract.Route}?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, state.CostQualityCalls);
    }
}
