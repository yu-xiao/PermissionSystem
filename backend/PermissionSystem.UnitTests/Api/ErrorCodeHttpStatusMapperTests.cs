using Microsoft.AspNetCore.Http;
using PermissionSystem.Api.Middlewares;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.UnitTests.Api;

public sealed class ErrorCodeHttpStatusMapperTests
{
    [Theory]
    [InlineData(ErrorCode.BadRequest, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCode.BusinessError, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCode.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorCode.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCode.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCode.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorCode.ValidationFailed, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(ErrorCode.TooManyRequests, StatusCodes.Status429TooManyRequests)]
    [InlineData(ErrorCode.InternalServerError, StatusCodes.Status500InternalServerError)]
    public void GetStatusCode_ShouldUseStableMapping(ErrorCode errorCode, int expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, ErrorCodeHttpStatusMapper.GetStatusCode(errorCode));
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_ShouldKeepApiResultAndTraceId()
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new BusinessException(ErrorCode.NotFound, "Resource was not found."),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GlobalExceptionMiddleware>.Instance,
            new TestHostEnvironment());
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-ea014";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var response = await System.Text.Json.JsonSerializer.DeserializeAsync<ApiResult>(
            context.Response.Body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.NotNull(response);
        Assert.False(response!.Succeeded);
        Assert.Equal((int)ErrorCode.NotFound, response.Code);
        Assert.Equal("trace-ea014", response.TraceId);
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Microsoft.Extensions.Hosting.Environments.Production;

        public string ApplicationName { get; set; } = "PermissionSystem.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
