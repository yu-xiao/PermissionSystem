using Microsoft.AspNetCore.Mvc;
using PermissionSystem.Api.Authorization;
using PermissionSystem.Application.Files;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Controllers;

[Route("api/files")]
public sealed class FileController : ApiControllerBase
{
    private readonly IFileService _fileService;

    public FileController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpGet]
    [Permission("system:file:view")]
    public async Task<ActionResult<ApiResult<PagedResult<FileResourceResponse>>>> GetPagedAsync(
        [FromQuery] FileResourceQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Success(await _fileService.GetPagedAsync(request, cancellationToken));
    }

    [HttpGet("business/{businessType}/{businessId:guid}")]
    [Permission("system:file:view")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<FileResourceResponse>>>> GetByBusinessAsync(
        string businessType,
        Guid businessId,
        CancellationToken cancellationToken)
    {
        return Success(await _fileService.GetByBusinessAsync(businessType, businessId, cancellationToken));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Permission("system:file:upload")]
    public async Task<ActionResult<ApiResult<FileResourceResponse>>> UploadAsync(
        IFormFile? file,
        [FromForm] string? businessType,
        [FromForm] Guid? businessId,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(ApiResult<FileResourceResponse>.Fail(
                ErrorCode.ValidationFailed,
                "File is required.",
                HttpContext.TraceIdentifier));
        }

        await using var stream = file.OpenReadStream();
        var result = await _fileService.UploadAsync(
            new UploadFileRequest
            {
                Content = stream,
                OriginalName = file.FileName,
                ContentType = file.ContentType,
                Size = file.Length,
                BusinessType = businessType,
                BusinessId = businessId
            },
            cancellationToken);

        return Success(result);
    }

    [HttpGet("{id:guid}/download")]
    [Permission("system:file:download")]
    public async Task<IActionResult> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var file = await _fileService.DownloadAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("{id:guid}")]
    [Permission("system:file:delete")]
    public async Task<ActionResult<ApiResult>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _fileService.DeleteAsync(id, cancellationToken);
        return Success();
    }
}
