using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Webhook.Options;
using Webhook.Services;

namespace Webhook.Tests;

public class ApiKeyMiddlewareTests
{
    [Fact]
    public async Task Webhooks_verification_get_without_credential_invokes_next()
    {
        bool nextCalled = false;
        var context = CreateContext("/webhooks", HttpMethods.Get);
        var middleware = CreateMiddleware(ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task Webhooks_without_credential_returns_401()
    {
        bool nextCalled = false;
        var context = CreateContext("/webhooks", HttpMethods.Post);
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.True(context.Response.Headers.ContainsKey(ApiKeyMiddleware.CorrelationIdHeaderName));
    }

    [Fact]
    public async Task Webhooks_with_invalid_credential_returns_403()
    {
        bool nextCalled = false;
        var context = CreateContext("/webhooks", HttpMethods.Post);
        context.Request.Headers[ApiKeyMiddleware.ApiKeyHeaderName] = "wrong-key";
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(ApiKeyMiddleware.ApiKeyHeaderName)]
    [InlineData(ApiKeyMiddleware.LegacyApiKeyHeaderName)]
    public async Task Webhooks_with_valid_credential_invokes_next(string headerName)
    {
        bool nextCalled = false;
        var context = CreateContext("/webhooks", HttpMethods.Post);
        context.Request.Headers[headerName] = "expected-key";
        context.Request.Headers[ApiKeyMiddleware.CorrelationIdHeaderName] = "cid-test-001";
        var middleware = CreateMiddleware(ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status202Accepted;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal("cid-test-001", context.Response.Headers[ApiKeyMiddleware.CorrelationIdHeaderName]);
    }

    [Fact]
    public async Task Id_clients_without_credential_still_returns_401()
    {
        bool nextCalled = false;
        var context = CreateContext("/id-clients", HttpMethods.Get);
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext(string path, string method)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-test-001",
            Response = { Body = new MemoryStream() }
        };

        context.Request.Path = path;
        context.Request.Method = method;
        return context;
    }

    private static ApiKeyMiddleware CreateMiddleware(RequestDelegate next)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AutoInventarioOptions
        {
            ApiKey = "expected-key"
        });

        return new ApiKeyMiddleware(next, options, NullLogger<ApiKeyMiddleware>.Instance);
    }
}
