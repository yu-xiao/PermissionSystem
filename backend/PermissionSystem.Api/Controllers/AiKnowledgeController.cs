using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.AiKnowledge;
using PermissionSystem.Application.Files;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/ai/knowledge")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiKnowledgeController(IAiKnowledgeService service) : ApiControllerBase
{
    [HttpGet("documents")]
    [Permission(AiCenterConstants.KnowledgeViewPermission)]
    public async Task<ActionResult<ApiResult<PagedResult<AiKnowledgeDocumentResponse>>>> List(
        CancellationToken ct, int pageIndex = 1, int pageSize = 20) => Success(await service.ListAsync(pageIndex, pageSize, ct));

    [HttpPost("documents")]
    [Permission(AiCenterConstants.KnowledgeManagePermission)]
    public async Task<ActionResult<ApiResult<AiKnowledgeDocumentResponse>>> Create(CreateAiKnowledgeDocumentRequest request, CancellationToken ct) =>
        Success(await service.CreateAsync(request, ct));

    [HttpPut("documents/{id:guid}/access")]
    [Permission(AiCenterConstants.KnowledgeManagePermission)]
    public async Task<ActionResult<ApiResult<AiKnowledgeDocumentResponse>>> SetAccess(Guid id, AiKnowledgeAccessRequest request, CancellationToken ct) =>
        Success(await service.SetAccessAsync(id, request, ct));

    [HttpPost("documents/{id:guid}/versions")]
    [RequestSizeLimit(AiKnowledgeContract.MaxFileBytes + 16 * 1024)]
    [Consumes("multipart/form-data")]
    [Permission(AiCenterConstants.KnowledgeManagePermission)]
    public async Task<ActionResult<ApiResult<AiKnowledgeDocumentResponse>>> Upload(Guid id, IFormFile file,
        [FromForm] DateTimeOffset validFrom, [FromForm] DateTimeOffset validUntil, [FromForm] string rowVersion, CancellationToken ct)
    {
        byte[] token;
        try { token = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { return BadRequest(ApiResult<AiKnowledgeDocumentResponse>.Fail(ErrorCode.ValidationFailed, "Invalid concurrency token.")); }
        await using var stream = file.OpenReadStream();
        return Success(await service.UploadAsync(id, new() { File = new UploadFileRequest { Content = stream, OriginalName = file.FileName,
            ContentType = file.ContentType, Size = file.Length }, ValidFrom = validFrom, ValidUntil = validUntil, RowVersion = token }, ct));
    }

    [HttpPost("documents/{id:guid}/versions/{versionId:guid}/publish")]
    [Permission(AiCenterConstants.KnowledgeManagePermission)]
    public async Task<ActionResult<ApiResult>> Publish(Guid id, Guid versionId, AiKnowledgePublishRequest request, CancellationToken ct)
    { await service.PublishAsync(id, versionId, request, ct); return Success(); }

    [HttpGet("documents/{id:guid}/versions/{versionId:guid}/preview")]
    [Permission(AiCenterConstants.KnowledgeQueryPermission)]
    public async Task<ActionResult<ApiResult<AiKnowledgeSearchResult>>> Preview(Guid id, Guid versionId, CancellationToken ct, int startSequence = 1) =>
        Success(await service.PreviewAsync(id, versionId, startSequence, ct));

    [HttpDelete("documents/{id:guid}")]
    [Permission(AiCenterConstants.KnowledgeManagePermission)]
    public async Task<ActionResult<ApiResult>> Delete(Guid id, AiKnowledgePublishRequest request, CancellationToken ct)
    { await service.DeleteAsync(id, request.RowVersion, ct); return Success(); }

    [HttpGet("search")]
    [Permission(AiCenterConstants.KnowledgeQueryPermission)]
    public async Task<ActionResult<ApiResult<AiKnowledgeSearchResult>>> Search([FromQuery] AiKnowledgeSearchRequest request, CancellationToken ct) =>
        Success(await service.SearchAsync(request, ct));

    [HttpGet("documents/{id:guid}/versions/{versionId:guid}/chunks/{chunkId:guid}")]
    [Permission(AiCenterConstants.KnowledgeQueryPermission)]
    public async Task<ActionResult<ApiResult<AiKnowledgeHit>>> Source(Guid id, Guid versionId, Guid chunkId, [FromQuery] string contentHash, CancellationToken ct) =>
        Success(await service.ReadChunkAsync(new(id, versionId, chunkId, contentHash), ct));
}
