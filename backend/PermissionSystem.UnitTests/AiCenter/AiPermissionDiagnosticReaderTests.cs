using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Application.Tenants;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Shared.Constants;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiPermissionDiagnosticReaderTests
{
    [Fact]
    public async Task Read_ShouldReturnOnlyOwnedServerEvidenceAndReauthorizeEachRead()
    {
        var fixture = new Fixture();
        var result = Assert.Single(await fixture.Reader.ReadAsync(fixture.Conversation.Id));
        Assert.Equal(fixture.Run.Id, result.RunId);
        Assert.Equal(PermissionDiagnosticConclusion.Denied, result.Data.Conclusion);
        fixture.Diagnostics.AllowRead = false;
        Assert.Empty(await fixture.Reader.ReadAsync(fixture.Conversation.Id));
        Assert.Equal(2, fixture.Diagnostics.ReadCount);
    }

    [Theory]
    [InlineData("assistant")]
    [InlineData("model-generated")]
    [InlineData("wrong-tenant")]
    [InlineData("wrong-conversation")]
    [InlineData("wrong-run")]
    [InlineData("other-actor")]
    [InlineData("wrong-tool")]
    [InlineData("failed-tool")]
    [InlineData("tampered-output")]
    [InlineData("unknown-version")]
    [InlineData("expired")]
    [InlineData("old-tool-json")]
    public async Task Read_ShouldRejectUntrustedOrIncompatibleEnvelopes(string scenario)
    {
        var fixture = new Fixture();
        switch (scenario)
        {
            case "assistant": fixture.Message.Role = AiMessageRole.Assistant; break;
            case "model-generated": fixture.Message.ModelGenerated = true; break;
            case "wrong-tenant": fixture.Message.TenantId = Guid.NewGuid(); break;
            case "wrong-conversation": fixture.Message.ConversationId = Guid.NewGuid(); break;
            case "wrong-run": fixture.Run.Id = Guid.NewGuid(); break;
            case "other-actor": fixture.Run.ActorUserId = Guid.NewGuid(); break;
            case "wrong-tool": fixture.Invocation.ToolCode = "other.tool"; break;
            case "failed-tool": fixture.Invocation.Status = AiInvocationStatus.Failed; break;
            case "tampered-output": fixture.Message.Content = fixture.Message.Content.Replace("Denied", "Allowed", StringComparison.Ordinal); break;
            case "unknown-version": fixture.Message.Content = fixture.Message.Content.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal); break;
            case "expired": fixture.Message.Content = "[expired]"; break;
            case "old-tool-json": fixture.Message.Content = "{\"items\":[]}"; break;
        }
        Assert.Empty(await fixture.Reader.ReadAsync(fixture.Conversation.Id));
        Assert.Equal(0, fixture.Diagnostics.ReadCount);
    }

    [Fact]
    public async Task Read_ShouldRejectCrossTenantContextAndUnownedConversation()
    {
        var fixture = new Fixture();
        fixture.Conversation.UserId = Guid.NewGuid();
        Assert.Empty(await fixture.Reader.ReadAsync(fixture.Conversation.Id));
        fixture.Conversation.UserId = TestIds.NormalUserId;
        fixture.Tenant.SetTenant(Guid.NewGuid(), "test");
        Assert.Empty(await fixture.Reader.ReadAsync(fixture.Conversation.Id));
    }

    private sealed class Fixture
    {
        public TenantContext Tenant { get; } = new();
        public AiConversation Conversation { get; } = new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, UserId = TestIds.NormalUserId };
        public AiRun Run { get; }
        public AiToolInvocation Invocation { get; }
        public AiMessage Message { get; }
        public TestDiagnostics Diagnostics { get; } = new();
        public AiPermissionDiagnosticReader Reader { get; }

        public Fixture()
        {
            Tenant.SetTenant(TestIds.TenantId, "test");
            Run = new AiRun { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, ActorUserId = TestIds.NormalUserId, ConversationId = Conversation.Id };
            var data = new PermissionDiagnosticResponse
            {
                Target = new() { Kind = PermissionDiagnosticKind.Permission, UserId = TestIds.NormalUserId, PermissionCode = "test:view" },
                Conclusion = PermissionDiagnosticConclusion.Denied,
                EvaluatedAt = DateTimeOffset.UtcNow,
                Summary = "Permission missing"
            };
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var content = JsonSerializer.Serialize(data, options);
            Invocation = new AiToolInvocation
            {
                Id = Guid.NewGuid(), TenantId = TestIds.TenantId, RunId = Run.Id, InvocationId = "diagnostic-1",
                ToolCode = PermissionDiagnosticAiToolHandler.ToolCode, ToolVersion = "1.0", Status = AiInvocationStatus.Completed,
                OutputDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            };
            Message = new AiMessage { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, ConversationId = Conversation.Id,
                Role = AiMessageRole.Tool, Content = JsonSerializer.Serialize(new AiPermissionDiagnosticEnvelope
                { RunId = Run.Id, InvocationId = Invocation.InvocationId, Data = data }, options) };
            Reader = new(new InMemoryRepository<AiMessage>(Message), new InMemoryRepository<AiRun>(Run),
                new InMemoryRepository<AiConversation>(Conversation), new InMemoryRepository<AiToolInvocation>(Invocation),
                new TestCurrentUserService(permissions: [AiCenterConstants.ConversationViewPermission]), Tenant,
                new InMemoryAsyncQueryExecutor(), Diagnostics);
        }
    }

    private sealed class TestDiagnostics : IPermissionDiagnosticService
    {
        public bool AllowRead { get; set; } = true;
        public int ReadCount { get; private set; }
        public Task<PermissionDiagnosticResponse> DiagnoseAsync(PermissionDiagnosticRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Historical evidence must not be recomputed.");
        public Task<bool> CanReadAsync(PermissionDiagnosticResponse result, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(AllowRead);
        }
    }
}
