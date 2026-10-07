using System.Text;
using Microsoft.EntityFrameworkCore;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Application.Files;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Infrastructure.Ai;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.UnitTests.TestSupport;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class Aic010KnowledgeTests
{
    [Fact]
    public async Task StorageFailure_IsSanitizedAndRetryReusesReservedVersion()
    {
        using var f = new Aic010TestFixture(); var doc = await f.Create(); f.Storage.FailSave = true;
        await Assert.ThrowsAsync<BusinessException>(() => f.Import(doc));
        var failed = (await f.Service.ListAsync(1, 20)).Items.Single();
        Assert.Equal("Failed", Assert.Single(failed.Versions).ParseStatus);
        Assert.Equal("knowledge_import_failed", failed.Versions[0].ErrorCode);
        Assert.Empty(f.Db.AiKnowledgeChunks);
        f.Storage.FailSave = false;
        var ready = await f.Import(failed);
        Assert.Equal(failed.Versions[0].Id, Assert.Single(ready.Versions).Id);
        Assert.Equal("Ready", ready.Versions[0].ParseStatus);
        Assert.Single(f.Db.AiKnowledgeChunks);
        Assert.Empty((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
    }

    [Fact]
    public async Task ScannerFailure_DoesNotPublishOrExposeStorageError()
    {
        using var f = new Aic010TestFixture(); var doc = await f.Create();
        var bytes = Encoding.UTF8.GetBytes("%PDF-synthetic scanner mismatch");
        using var stream = new MemoryStream(bytes);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.UploadAsync(doc.Id, new()
        {
            RowVersion = doc.RowVersion, ValidFrom = DateTimeOffset.UtcNow, ValidUntil = DateTimeOffset.UtcNow.AddDays(1),
            File = new UploadFileRequest { Content = stream, Size = bytes.Length, OriginalName = "synthetic.txt", ContentType = "text/plain" }
        }));
        var failed = (await f.Service.ListAsync(1, 20)).Items.Single();
        Assert.Equal("knowledge_import_failed", failed.Versions[0].ErrorCode);
        Assert.Empty(f.Db.AiKnowledgeChunks); Assert.Null(failed.CurrentVersionId);
    }

    [Fact]
    public async Task PendingVersion_CannotBePublishedOrSearched()
    {
        using var f = new Aic010TestFixture(); var doc = await f.Import(await f.Create());
        f.Db.AiKnowledgeDocumentVersions.Single().ParseStatus = AiKnowledgeParseStatus.Pending;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.PublishAsync(doc.Id, doc.Versions[0].Id, new() { RowVersion = doc.RowVersion }));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.PreviewAsync(doc.Id, doc.Versions[0].Id));
        Assert.Empty((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
    }

    [Fact]
    public async Task FutureVersion_IsReviewableButNotYetSearchable_AndPreviewIsPaged()
    {
        using var f = new Aic010TestFixture(); var doc = await f.Import(await f.Create(),
            string.Join("\n\n", Enumerable.Range(1, 7).Select(i => $"请假合成段落{i}")));
        f.Db.AiKnowledgeDocumentVersions.Single().ValidFrom = DateTimeOffset.UtcNow.AddHours(1);
        await f.Db.SaveChangesAsync();
        var preview = await f.Service.PreviewAsync(doc.Id, doc.Versions[0].Id);
        Assert.Equal(5, preview.Items.Count); Assert.True(preview.IsTruncated);
        Assert.Equal(2, (await f.Service.PreviewAsync(doc.Id, doc.Versions[0].Id, 6)).Items.Count);
        await f.Service.PublishAsync(doc.Id, doc.Versions[0].Id, new() { RowVersion = doc.RowVersion });
        Assert.Empty((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
    }

    [Fact]
    public async Task CapacityLimit_DoesNotCreateThe101stActiveDocument()
    {
        using var f = new Aic010TestFixture();
        for (var i = 0; i < 100; i++) await f.Create($"合成{i}");
        await Assert.ThrowsAsync<BusinessException>(() => f.Create());
        Assert.Equal(100, f.Db.AiKnowledgeDocuments.Count());
    }

    [Theory]
    [InlineData("请假规则\n\n请假申请需要负责人确认。", 2, 3)]
    [InlineData("标题\r\n\r\n中文依据", 2, 3)]
    [InlineData("单行资料", 1, 1)]
    public async Task Parser_PreservesVerifiableLineLocations(string text, int count, int lastStartLine)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var parsed = await new AiKnowledgeTextParser().ParseAsync(stream);
        Assert.Equal(count, parsed.Count); Assert.Equal(lastStartLine, parsed.Last().StartLine);
        Assert.All(parsed, p => Assert.Equal(AiStructuredResults.Digest(p.Content), p.ContentHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n\t")]
    [InlineData("text\0binary")]
    public async Task Parser_RejectsEmptyAndBinaryText(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await Assert.ThrowsAsync<BusinessException>(() => new AiKnowledgeTextParser().ParseAsync(stream));
    }

    [Fact]
    public async Task Parser_RejectsMalformedUtf8AndExcessBytes()
    {
        using var invalid = new MemoryStream([0xC3, 0x28]);
        await Assert.ThrowsAsync<BusinessException>(() => new AiKnowledgeTextParser().ParseAsync(invalid));
        using var oversized = new MemoryStream(new byte[AiKnowledgeContract.MaxFileBytes + 1]);
        await Assert.ThrowsAsync<BusinessException>(() => new AiKnowledgeTextParser().ParseAsync(oversized));
    }

    [Fact]
    public async Task Parser_SplitsLongLinesWithoutBreakingSurrogates()
    {
        var text = string.Concat(Enumerable.Repeat("😀", 1600));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var parsed = await new AiKnowledgeTextParser().ParseAsync(stream);
        Assert.Equal(text, string.Concat(parsed.Select(p => p.Content)));
        Assert.All(parsed, p => { Assert.InRange(p.Content.Length, 1, 2000); Assert.False(char.IsHighSurrogate(p.Content[^1])); });
    }

    [Fact]
    public async Task Parser_RejectsMoreThan256Chunks()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n\n", Enumerable.Repeat("合成", 257))));
        await Assert.ThrowsAsync<BusinessException>(() => new AiKnowledgeTextParser().ParseAsync(stream));
    }

    [Fact]
    public async Task ImportAndPublication_KeepDraftOutOfSearchAndExposeOnlyCurrentVersion()
    {
        using var f = new Aic010TestFixture(); var draft = await f.Import(await f.Create());
        Assert.Empty((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
        Assert.Single((await f.Service.PreviewAsync(draft.Id, draft.Versions[0].Id)).Items);
        await f.Service.PublishAsync(draft.Id, draft.Versions[0].Id, new() { RowVersion = draft.RowVersion });
        var first = Assert.Single((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
        Assert.Equal(1, first.VersionNumber); Assert.Equal(first, await f.Service.ReadChunkAsync(first.Reference));
        var metadata = (await f.Service.ListAsync(1, 50)).Items[0];
        var second = await f.Import(metadata, "请假申请必须重新确认。 ");
        await f.Service.PublishAsync(second.Id, second.Versions[0].Id, new() { RowVersion = second.RowVersion });
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReadChunkAsync(first.Reference));
        Assert.Equal(2, Assert.Single((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items).VersionNumber);
    }

    [Fact]
    public async Task DuplicateContent_ReusesVersionAndFile()
    {
        using var f = new Aic010TestFixture(); var doc = await f.Import(await f.Create());
        await f.Import(doc);
        Assert.Single(f.Db.AiKnowledgeDocumentVersions); Assert.Single(f.Db.FileResources); Assert.Single(f.Db.AiKnowledgeChunks);
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("role")]
    [InlineData("membership")]
    [InlineData("expired")]
    [InlineData("file")]
    [InlineData("identity")]
    public async Task RevocationAndExpiry_BlockSearchSourceAndFile(string change)
    {
        using var f = new Aic010TestFixture(); var doc = await f.Published();
        var hit = Assert.Single((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
        var fileId = f.Db.FileResources.Single().Id;
        switch (change)
        {
            case "grant": await f.Service.SetAccessAsync(doc.Id, new() { RowVersion = doc.RowVersion, RoleIds = [] }); break;
            case "role": f.Db.Roles.Single().IsEnabled = false; break;
            case "membership": f.Db.UserRoles.Single().IsDeleted = true; break;
            case "expired": f.Db.AiKnowledgeDocumentVersions.Single().ValidUntil = DateTimeOffset.UtcNow.AddSeconds(-1); break;
            case "file": f.Db.FileResources.Single().FileStatus = FileStatus.PendingDelete; break;
            case "identity": f.Identities.Active = false; break;
        }
        await f.Db.SaveChangesAsync();
        if (change == "identity") await Assert.ThrowsAsync<BusinessException>(() => f.Service.SearchAsync(new() { Keyword = "请假" }));
        else Assert.Empty((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.ReadChunkAsync(hit.Reference));
        await Assert.ThrowsAsync<BusinessException>(() => f.Files.DownloadAsync(fileId));
        Assert.Empty((await f.Files.GetPagedAsync(new())).Items);
    }

    [Fact]
    public async Task SuperAdmin_StillRequiresExplicitDocumentRoleGrant()
    {
        using var f = new Aic010TestFixture(superAdmin: true);
        var draft = await f.Import(await f.Create(roleIds: []));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.PublishAsync(draft.Id, draft.Versions[0].Id, new() { RowVersion = draft.RowVersion }));
        Assert.Empty((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
    }

    [Fact]
    public async Task ManagerPermission_DoesNotPermitBodyReading()
    {
        using var f = new Aic010TestFixture([AiCenterConstants.KnowledgeViewPermission, AiCenterConstants.KnowledgeManagePermission]);
        var doc = await f.Import(await f.Create());
        Assert.Single((await f.Service.ListAsync(1, 50)).Items);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.PreviewAsync(doc.Id, doc.Versions[0].Id));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SearchAsync(new() { Keyword = "请假" }));
        var metadata = System.Text.Json.JsonSerializer.Serialize(doc);
        Assert.DoesNotContain("负责人确认", metadata);
    }

    [Fact]
    public async Task CrossTenantDocumentRolesAndTopK_CannotCrowdOutVisibleResults()
    {
        using var f = new Aic010TestFixture(); var allowed = await f.Published();
        for (var i = 0; i < 6; i++)
        {
            var doc = await f.Published("请假隐蔽资料", "A" + i);
            await f.Service.SetAccessAsync(doc.Id, new() { RoleIds = [], RowVersion = doc.RowVersion });
        }
        var result = await f.Service.SearchAsync(new() { Keyword = "请假", Limit = 1 });
        Assert.Equal(allowed.Id, Assert.Single(result.Items).Reference.DocumentId); Assert.False(result.IsTruncated);
        f.Tenant.SetTenant(Guid.NewGuid(), "other");
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SearchAsync(new() { Keyword = "请假" }));
    }

    [Fact]
    public async Task StaleTokensCrossTenantRolesAndRealDataAreRejected()
    {
        using var f = new Aic010TestFixture(); var doc = await f.Create();
        await f.Service.SetAccessAsync(doc.Id, new() { RowVersion = doc.RowVersion, RoleIds = [] });
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.SetAccessAsync(doc.Id, new() { RowVersion = doc.RowVersion, RoleIds = [f.RoleId] }));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.CreateAsync(new() { Title = "x", Owner = "x", License = "x", Synthetic = false }));
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.CreateAsync(new() { Title = "x", Owner = "x", License = "x", Synthetic = true, RoleIds = [Guid.NewGuid()] }));
    }

    [Fact]
    public async Task DefaultOffAndInvalidSearchArgumentsAreRejected()
    {
        using var disabled = new Aic010TestFixture(enabled: false);
        await Assert.ThrowsAsync<BusinessException>(() => disabled.Service.SearchAsync(new() { Keyword = "请假" }));
        using var enabled = new Aic010TestFixture();
        foreach (var request in new AiKnowledgeSearchRequest[] { new(), new() { Keyword = "x", Limit = 6 }, new() { Keyword = "x", DocumentId = Guid.Empty } })
            await Assert.ThrowsAsync<BusinessException>(() => enabled.Service.SearchAsync(request));
    }

    [Fact]
    public async Task RawFileEntrypoints_HideStorageAndRejectDirectDeletion()
    {
        using var f = new Aic010TestFixture(); await f.Published();
        var fileId = f.Db.FileResources.Single().Id;
        var item = Assert.Single((await f.Files.GetPagedAsync(new())).Items);
        Assert.Empty(item.ObjectKey); Assert.Empty(item.BucketName); Assert.Null(item.LastError);
        await Assert.ThrowsAsync<BusinessException>(() => f.Files.DeleteAsync(fileId));
        await using var download = (await f.Files.DownloadAsync(fileId)).Content;
        Assert.True(download.Length > 0);
    }

    [Fact]
    public async Task Delete_PurgesChunksAndAnswersWhileSchedulingFileCompensation()
    {
        using var f = new Aic010TestFixture(); var doc = await f.Published();
        var hit = Assert.Single((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
        var run = new AiRun { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, ActorUserId = TestIds.NormalUserId,
            ConversationId = Guid.NewGuid(), ResponseMessageId = Guid.NewGuid() };
        f.Db.AiRuns.Add(run);
        f.Db.AiMessages.Add(new() { Id = run.ResponseMessageId.Value, TenantId = TestIds.TenantId, ConversationId = run.ConversationId,
            Content = "请假正文", Role = AiMessageRole.Assistant, Sequence = 1 });
        var unrelatedEnvelope = AiStructuredResults.SerializeBounded(new() { RunId = Guid.NewGuid(), InvocationId = "other-run",
            Type = "knowledge-citations", ToolCode = AiKnowledgeContract.ToolCode, ToolVersion = "1.0", KnowledgeReferences = [] });
        f.Db.AiMessages.Add(new() { TenantId = TestIds.TenantId, ConversationId = run.ConversationId,
            Content = unrelatedEnvelope, ContentDigest = AiStructuredResults.Digest(unrelatedEnvelope), Role = AiMessageRole.Tool, Sequence = 2 });
        f.Db.AiKnowledgeRunReferences.Add(new() { Id = Guid.NewGuid(), TenantId = TestIds.TenantId, RunId = run.Id,
            InvocationId = "synthetic", DocumentId = doc.Id, VersionId = hit.Reference.VersionId, ChunkId = hit.Reference.ChunkId, ContentHash = hit.Reference.ContentHash });
        await f.Db.SaveChangesAsync();
        await f.Service.DeleteAsync(doc.Id, doc.RowVersion);
        Assert.Empty((await f.Service.SearchAsync(new() { Keyword = "请假" })).Items);
        Assert.Equal("", f.Db.AiKnowledgeChunks.IgnoreQueryFilters().Single().Content);
        Assert.Equal("[expired]", f.Db.AiMessages.Single(m => m.Role == AiMessageRole.Assistant).Content);
        Assert.Equal(unrelatedEnvelope, f.Db.AiMessages.Single(m => m.Role == AiMessageRole.Tool).Content);
        Assert.Equal(FileStatus.PendingDelete, f.Db.FileResources.Single().FileStatus);
        Assert.Single(f.Db.AiKnowledgeRunReferences);
    }

    [Fact]
    public async Task Tool_RejectsIdentityParametersAndPersistsReferencesWithoutBody()
    {
        using var f = new Aic010TestFixture(); await f.Published();
        var tool = new KnowledgeDocumentSearchAiToolHandler(f.Service, f.Options, f.QueryGuard);
        var context = new AiToolExecutionContext { TenantId = TestIds.TenantId, ActorUserId = TestIds.NormalUserId };
        await Assert.ThrowsAsync<BusinessException>(() => tool.ExecuteAsync(context, "{\"keyword\":\"请假\",\"tenantId\":\"invalid\"}"));
        var output = await tool.ExecuteAsync(context, "{\"keyword\":\"请假\"}");
        Assert.Contains("负责人确认", Assert.Single(output.KnowledgeHits!).Content);
        var envelope = AiStructuredResults.SerializeBounded(output.StructuredResult!);
        Assert.DoesNotContain("负责人确认", envelope); Assert.Single(output.StructuredResult!.KnowledgeReferences!);
    }
}
