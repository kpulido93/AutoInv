using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Webhook.Services;

namespace Webhook.Tests;

public class ApiKeyMiddlewareTests
{
    [Fact]
    public async Task Webhooks_without_credential_returns_401()
    {
        bool nextCalled = false;
        var context = CreateContext("/webhooks");
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
        var context = CreateContext("/webhooks");
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
        var context = CreateContext("/webhooks");
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

    private static DefaultHttpContext CreateContext(string path)
    {
        return new DefaultHttpContext
        {
            TraceIdentifier = "trace-test-001",
            Request = { Path = path },
            Response = { Body = new MemoryStream() }
        };
    }

    private static ApiKeyMiddleware CreateMiddleware(RequestDelegate next)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AutoInventario:ApiKey"] = "expected-key"
            })
            .Build();

        return new ApiKeyMiddleware(next, config, NullLogger<ApiKeyMiddleware>.Instance);
    }
}
