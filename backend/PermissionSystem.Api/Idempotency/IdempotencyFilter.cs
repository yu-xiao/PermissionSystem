using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Idempotency;

public sealed class IdempotencyFilter : IAsyncResourceFilter, IOrderedFilter
{
    private const string HeaderName = "X-Idempotency-Key";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IIdempotencyService _idempotencyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(
        IIdempotencyService idempotencyService,
        ICurrentUserService currentUserService,
        ILogger<IdempotencyFilter> logger)
    {
        _idempotencyService = idempotencyService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public int Order => -2000;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var attribute = context.ActionDescriptor.EndpointMetadata.OfType<IdempotencyKeyAttribute>().FirstOrDefault();
        if (attribute is null || IsReadOnlyRequest(context.HttpContext.Request))
        {
            await next();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var headerValue) ||
            string.IsNullOrWhiteSpace(headerValue))
        {
            context.Result = new BadRequestObjectResult(ApiResult.Fail(
                ErrorCode.ValidationFailed,
                "X-Idempotency-Key header is required.",
                context.HttpContext.TraceIdentifier));
            return;
        }

        var request = context.HttpContext.Request;
        var requestBodyHash = await CalculateRequestBodyHashAsync(request, context.HttpContext.RequestAborted);
        var idempotencyKey = BuildScopedKey(context.HttpContext, headerValue.ToString());
        var expiresIn = TimeSpan.FromSeconds(attribute.ExpirationSeconds);
        var cachedEntry = await _idempotencyService.GetAsync(idempotencyKey, context.HttpContext.RequestAborted);
        if (cachedEntry is not null)
        {
            if (!RequestMatches(cachedEntry, request, requestBodyHash))
            {
                context.Result = BuildRequestMismatchResult(context.HttpContext);
                return;
            }

            if (cachedEntry.State == "Completed")
            {
                _logger.LogWarning(
                    "Duplicate idempotent request replayed. UserId: {UserId}, Path: {Path}, Key: {Key}",
                    _currentUserService.UserId,
                    request.Path,
                    headerValue.ToString());
                context.Result = ToContentResult(cachedEntry);
                return;
            }
        }

        var operationId = Guid.NewGuid().ToString("N");
        var processingEntry = new IdempotencyCacheEntry
        {
            State = "Processing",
            OperationId = operationId,
            Method = request.Method.ToUpperInvariant(),
            Path = GetRequestPath(request),
            RequestBodyHash = requestBodyHash,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(expiresIn)
        };
        var began = await _idempotencyService.TryBeginAsync(
            idempotencyKey,
            processingEntry,
            expiresIn,
            context.HttpContext.RequestAborted);
        if (!began)
        {
            cachedEntry = await _idempotencyService.GetAsync(idempotencyKey, context.HttpContext.RequestAborted);
            if (cachedEntry is not null && !RequestMatches(cachedEntry, request, requestBodyHash))
            {
                context.Result = BuildRequestMismatchResult(context.HttpContext);
                return;
            }

            if (cachedEntry?.State == "Completed")
            {
                context.Result = ToContentResult(cachedEntry);
                return;
            }

            _logger.LogWarning(
                "Duplicate idempotent request is still processing. UserId: {UserId}, Path: {Path}, Key: {Key}",
                _currentUserService.UserId,
                request.Path,
                headerValue.ToString());
            context.Result = new ConflictObjectResult(ApiResult.Fail(
                ErrorCode.Conflict,
                "Duplicate request is already processing.",
                context.HttpContext.TraceIdentifier));
            return;
        }

        var executedContext = await next();
        if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
        {
            await _idempotencyService.RemoveAsync(idempotencyKey, operationId, context.HttpContext.RequestAborted);
            return;
        }

        var entry = ToCacheEntry(executedContext.Result, context.HttpContext, processingEntry, expiresIn);
        if (entry.StatusCode is >= 200 and < 300)
        {
            var stored = await _idempotencyService.StoreAsync(
                idempotencyKey,
                operationId,
                entry,
                expiresIn,
                context.HttpContext.RequestAborted);
            if (!stored)
            {
                _logger.LogWarning(
                    "Idempotent response was not cached because request ownership changed. Path: {Path}, TraceId: {TraceId}",
                    request.Path,
                    context.HttpContext.TraceIdentifier);
            }

            return;
        }

        await _idempotencyService.RemoveAsync(idempotencyKey, operationId, context.HttpContext.RequestAborted);
    }

    private string BuildScopedKey(HttpContext context, string idempotencyKey)
    {
        var userPart = _currentUserService.UserId?.ToString("N") ??
            _currentUserService.Username ??
            "anonymous";
        var tenantPart = _currentUserService.TenantId?.ToString("N") ?? "default";
        var rawKey = string.Join(
            '|',
            tenantPart,
            userPart,
            idempotencyKey.Trim());

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
    }

    private static async Task<string> CalculateRequestBodyHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is 0 || !request.Body.CanRead)
        {
            return Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant();
        }

        request.EnableBuffering();
        request.Body.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await request.Body.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, bytesRead);
        }

        request.Body.Position = 0;
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool RequestMatches(IdempotencyCacheEntry entry, HttpRequest request, string requestBodyHash)
    {
        return string.Equals(entry.Method, request.Method, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Path, GetRequestPath(request), StringComparison.Ordinal) &&
            string.Equals(entry.RequestBodyHash, requestBodyHash, StringComparison.Ordinal);
    }

    private static string GetRequestPath(HttpRequest request)
    {
        return string.Concat(request.PathBase.Value, request.Path.Value, request.QueryString.Value).ToLowerInvariant();
    }

    private static ConflictObjectResult BuildRequestMismatchResult(HttpContext context)
    {
        return new ConflictObjectResult(ApiResult.Fail(
            ErrorCode.Conflict,
            "X-Idempotency-Key has already been used for a different request.",
            context.TraceIdentifier));
    }

    private static bool IsReadOnlyRequest(HttpRequest request)
    {
        return HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method);
    }

    private static ContentResult ToContentResult(IdempotencyCacheEntry entry)
    {
        return new ContentResult
        {
            StatusCode = entry.StatusCode,
            ContentType = entry.ContentType,
            Content = entry.Body
        };
    }

    private static IdempotencyCacheEntry ToCacheEntry(
        IActionResult? result,
        HttpContext context,
        IdempotencyCacheEntry processingEntry,
        TimeSpan expiresIn)
    {
        var statusCode = GetStatusCode(result, context);
        var contentType = "application/json; charset=utf-8";
        var body = result switch
        {
            ObjectResult objectResult => JsonSerializer.Serialize(objectResult.Value, JsonOptions),
            JsonResult jsonResult => JsonSerializer.Serialize(jsonResult.Value, JsonOptions),
            ContentResult contentResult => contentResult.Content ?? string.Empty,
            _ => string.Empty
        };

        if (result is ContentResult contentResultValue && !string.IsNullOrWhiteSpace(contentResultValue.ContentType))
        {
            contentType = contentResultValue.ContentType;
        }

        return new IdempotencyCacheEntry
        {
            State = "Completed",
            OperationId = processingEntry.OperationId,
            Method = processingEntry.Method,
            Path = processingEntry.Path,
            RequestBodyHash = processingEntry.RequestBodyHash,
            StatusCode = statusCode,
            ContentType = contentType,
            Body = body,
            ResponseBodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(expiresIn)
        };
    }

    private static int GetStatusCode(IActionResult? result, HttpContext context)
    {
        return result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? StatusCodes.Status200OK,
            JsonResult jsonResult => jsonResult.StatusCode ?? StatusCodes.Status200OK,
            ContentResult contentResult => contentResult.StatusCode ?? StatusCodes.Status200OK,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => context.Response.StatusCode == StatusCodes.Status200OK
                ? StatusCodes.Status200OK
                : context.Response.StatusCode
        };
    }
}
