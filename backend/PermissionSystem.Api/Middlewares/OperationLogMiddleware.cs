using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.WebUtilities;
using PermissionSystem.Api.Services;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.OperationLogs;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Api.Middlewares;

public sealed class OperationLogMiddleware
{
    private const int BodyMaxLength = 4000;
    private static readonly int ResponseCaptureMaxBytes = Encoding.UTF8.GetMaxByteCount(BodyMaxLength);
    private static readonly Guid AnonymousTenantId = Guid.Empty;
    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "oldPassword",
        "newPassword",
        "confirmPassword",
        "apiKey",
        "apiSecret",
        "verifyCode",
        "verificationCode",
        "access_token",
        "refresh_token",
        "id_token",
        "login_code",
        "code_verifier",
        "client_secret"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<OperationLogMiddleware> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public OperationLogMiddleware(
        RequestDelegate next,
        ILogger<OperationLogMiddleware> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _next = next;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        ITraceContextAccessor traceContextAccessor,
        IClientIpAccessor clientIpAccessor)
    {
        if (!ShouldLog(context.Request))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var originalResponseBody = context.Response.Body;

        await using var responseBody = new ResponseCaptureStream(originalResponseBody, ResponseCaptureMaxBytes);
        context.Response.Body = responseBody;

        Exception? exception = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ResolveExceptionStatusCode(ex);
            }
        }
        finally
        {
            stopwatch.Stop();

            await responseBody.FlushAsync();
            context.Response.Body = originalResponseBody;
            var responseText = ReadResponseBody(context.Response, responseBody);

            await CreateOperationLogAsync(
                context,
                currentUserService,
                tenantContext.TenantId,
                traceContextAccessor,
                clientIpAccessor.GetClientIp(context),
                requestBody,
                responseText,
                stopwatch.ElapsedMilliseconds);
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private async Task CreateOperationLogAsync(
        HttpContext context,
        ICurrentUserService currentUserService,
        Guid? targetTenantId,
        ITraceContextAccessor traceContextAccessor,
        string clientIp,
        string? requestBody,
        string? responseBody,
        long elapsedMilliseconds)
    {
        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var operationTenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            if (targetTenantId.HasValue && targetTenantId.Value != Guid.Empty)
            {
                operationTenantContext.SetTenant(targetTenantId.Value, "Request");
            }

            var operationLogService = scope.ServiceProvider.GetRequiredService<IOperationLogService>();
            var endpoint = context.GetEndpoint();
            var actionDescriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var controllerName = actionDescriptor?.ControllerName ?? GetModuleFromPath(context.Request.Path);
            var actionName = actionDescriptor?.ActionName ?? context.Request.Method;

            await operationLogService.CreateAsync(new CreateOperationLogRequest
            {
                TenantId = targetTenantId ?? currentUserService.TenantId ?? AnonymousTenantId,
                UserId = currentUserService.UserId,
                UserName = currentUserService.Username,
                Module = controllerName,
                Action = actionName,
                Method = $"{controllerName}.{actionName}",
                RequestPath = context.Request.Path.Value,
                RequestMethod = context.Request.Method.ToUpperInvariant(),
                RequestBody = requestBody,
                ResponseBody = responseBody,
                IpAddress = clientIp,
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                StatusCode = context.Response.StatusCode,
                ElapsedMilliseconds = elapsedMilliseconds,
                TraceId = ResolveTraceId(context, traceContextAccessor)
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write operation log.");
        }
    }

    private static int ResolveExceptionStatusCode(Exception exception)
    {
        return exception is BusinessException businessException
            ? ErrorCodeHttpStatusMapper.GetStatusCode(businessException.ErrorCode)
            : ErrorCodeHttpStatusMapper.GetStatusCode(ErrorCode.InternalServerError);
    }

    private static string ResolveTraceId(HttpContext context, ITraceContextAccessor traceContextAccessor)
    {
        return !string.IsNullOrWhiteSpace(traceContextAccessor.TraceId)
            ? traceContextAccessor.TraceId
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    private static bool ShouldLog(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api"))
        {
            return false;
        }

        return !request.Path.StartsWithSegments("/api/health");
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or <= 0 || IsBinaryContent(request.ContentType))
        {
            return null;
        }

        request.EnableBuffering();

        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        return SanitizeAndTruncate(body, request.ContentType);
    }

    private static string? ReadResponseBody(HttpResponse response, ResponseCaptureStream responseBody)
    {
        if (responseBody.IsTruncated || IsBinaryContent(response.ContentType))
        {
            return null;
        }

        return SanitizeAndTruncate(responseBody.GetCapturedText(), response.ContentType);
    }

    internal static string? SanitizeAndTruncate(string? value, string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = IsFormUrlEncoded(contentType)
            ? TryRedactFormUrlEncoded(value)
            : TryRedactJson(value) ?? value;
        if (sanitized is null)
        {
            return null;
        }

        return sanitized.Length <= BodyMaxLength
            ? sanitized
            : sanitized[..BodyMaxLength];
    }

    private static string? TryRedactFormUrlEncoded(string value)
    {
        try
        {
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in QueryHelpers.ParseQuery(value))
            {
                fields[pair.Key] = IsSensitiveField(pair.Key)
                    ? "***"
                    : pair.Value.Count == 1
                        ? pair.Value[0]
                        : pair.Value.ToArray();
            }

            return JsonSerializer.Serialize(fields);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsFormUrlEncoded(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryRedactJson(string value)
    {
        try
        {
            var node = JsonNode.Parse(value);
            if (node is null)
            {
                return null;
            }

            RedactNode(node);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void RedactNode(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToList())
            {
                if (IsSensitiveField(property.Key))
                {
                    jsonObject[property.Key] = "***";
                    continue;
                }

                if (property.Value is not null)
                {
                    RedactNode(property.Value);
                }
            }
        }

        if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                if (item is not null)
                {
                    RedactNode(item);
                }
            }
        }
    }

    private static bool IsSensitiveField(string fieldName)
    {
        return SensitiveFieldNames.Contains(fieldName) ||
            fieldName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("apiKey", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("token", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBinaryContent(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("image/", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("video/", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetModuleFromPath(PathString path)
    {
        var segments = path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments is { Length: > 1 } ? segments[1] : "Api";
    }
}

internal sealed class ResponseCaptureStream : Stream
{
    private readonly Stream _inner;
    private readonly MemoryStream _capture = new();
    private readonly int _captureLimit;
    private bool _isTruncated;

    public ResponseCaptureStream(Stream inner, int captureLimit)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(captureLimit);

        _inner = inner;
        _captureLimit = captureLimit;
    }

    public bool IsTruncated => _isTruncated;

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => _inner.CanWrite;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public string GetCapturedText()
    {
        return Encoding.UTF8.GetString(_capture.GetBuffer(), 0, checked((int)_capture.Length));
    }

    public override void Flush()
    {
        _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _inner.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        Capture(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        Capture(buffer);
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        await _inner.WriteAsync(buffer, offset, count, cancellationToken);
        Capture(buffer.AsSpan(offset, count));
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken);
        Capture(buffer);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _capture.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Capture(ReadOnlyMemory<byte> buffer)
    {
        Capture(buffer.Span);
    }

    private void Capture(ReadOnlySpan<byte> buffer)
    {
        if (_isTruncated || buffer.IsEmpty)
        {
            return;
        }

        var remaining = _captureLimit - _capture.Length;
        if (remaining <= 0)
        {
            _isTruncated = true;
            return;
        }

        var count = (int)Math.Min(remaining, buffer.Length);
        _capture.Write(buffer[..count]);
        if (count < buffer.Length)
        {
            _isTruncated = true;
        }
    }
}
