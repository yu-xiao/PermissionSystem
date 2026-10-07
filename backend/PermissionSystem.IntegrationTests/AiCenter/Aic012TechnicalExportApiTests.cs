using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.IntegrationTests.AiCenter;

public sealed class Aic012TechnicalExportApiTests
{
    [Theory]
    [InlineData(false, false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, false, HttpStatusCode.Forbidden)]
    [InlineData(true, true, false, HttpStatusCode.Forbidden)]
    [InlineData(true, false, true, HttpStatusCode.Forbidden)]
    [InlineData(true, true, true, HttpStatusCode.OK)]
    public async Task Export_RequiresBothPermissionsAndUsesPrivateAttachment(bool authenticated, bool view, bool export, HttpStatusCode expected)
    {
        var state = new Aic012OperationsApiTests.ContractService();
        using var server = await Aic012OperationsApiTests.Server(state); using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", authenticated ? "true" : "false");
        if (view) client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.OperationsViewPermission);
        if (export) client.DefaultRequestHeaders.Add("X-Test-Permission", AiCenterConstants.OperationsExportPermission);
        using var response = await client.PostAsJsonAsync(AiTechnicalExportContract.Route, new AiTechnicalExportRequest());
        Assert.Equal(expected, response.StatusCode); Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, state.ExportCalls);
        if (expected != HttpStatusCode.OK) return;
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("Content-Disposition", Assert.Single(response.Headers.GetValues("Access-Control-Expose-Headers")));
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Matches("^ai-technical-[a-f0-9]{32}\\.json$", response.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        Assert.Equal("{}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Export_BindsOnlyWindowAndRejectsMalformedDates()
    {
        var state = new Aic012OperationsApiTests.ContractService();
        using var server = await Aic012OperationsApiTests.Server(state); using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        client.DefaultRequestHeaders.Add("X-Test-Permission", new[] { AiCenterConstants.OperationsViewPermission, AiCenterConstants.OperationsExportPermission });
        using var ok = await client.PostAsJsonAsync(AiTechnicalExportContract.Route,
            new { from = "2026-10-01T00:00:00+08:00", to = "2026-10-02T00:00:00+08:00", tenantId = Guid.NewGuid(), fields = new[] { "Content" } });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T00:00:00+08:00"), state.ExportRequest!.From);
        Assert.Equal(new[] { "From", "To" }, typeof(AiTechnicalExportRequest).GetProperties().Select(p => p.Name));
        using var invalid = await client.PostAsJsonAsync(AiTechnicalExportContract.Route, new { from = "bad" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.Equal(1, state.ExportCalls);
    }
}
