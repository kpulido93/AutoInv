using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Webhook.Services
{
    /// <summary>
    /// Middleware simple para proteger rutas sensibles mediante una API Key.
    /// 
    /// Protege:
    ///  - /id-clients
    ///  - /webhooks
    /// 
    /// Header requerido: X-AutoInventario-Key
    /// Valor: configurado en appsettings.json (AutoInventario:ApiKey) o variable de entorno.
    /// </summary>
    public sealed class ApiKeyMiddleware
    {
        public const string ApiKeyHeaderName = "X-AutoInventario-Key";
        public const string LegacyApiKeyHeaderName = "x-api-key";
        public const string CorrelationIdHeaderName = "X-Correlation-ID";

        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;
        private readonly ILogger<ApiKeyMiddleware> _logger;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration config, ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _config = config;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = ResolveCorrelationId(context);
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            context.Items[CorrelationIdHeaderName] = correlationId;

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            });

            var path = context.Request.Path.Value ?? string.Empty;

            bool needsKey =
                path.Equals("/id-clients", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/webhooks", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/webhooks/", StringComparison.OrdinalIgnoreCase);

            if (!needsKey)
            {
                await _next(context);
                return;
            }

            var expected = _config["AutoInventario:ApiKey"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                _logger.LogError("API key middleware misconfigured: AutoInventario:ApiKey is not set.");
                await context.Response.WriteAsync("Server misconfigured.");
                return;
            }

            if (!TryGetProvidedApiKey(context, out var provided))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                _logger.LogWarning("Request rejected: missing API key. Path: {Path}", path);
                await context.Response.WriteAsync("Missing API key.");
                return;
            }

            // Comparación en tiempo constante (evita timing attacks triviales)
            if (!FixedTimeEquals(provided, expected))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                _logger.LogWarning("Request rejected: invalid API key. Path: {Path}", path);
                await context.Response.WriteAsync("Invalid API key.");
                return;
            }

            await _next(context);
        }

        private static string ResolveCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var values))
            {
                var provided = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(provided))
                    return provided;
            }

            return context.TraceIdentifier;
        }

        private static bool TryGetProvidedApiKey(HttpContext context, out string provided)
        {
            provided = string.Empty;

            if (TryGetHeader(context.Request.Headers, ApiKeyHeaderName, out provided))
                return true;

            return TryGetHeader(context.Request.Headers, LegacyApiKeyHeaderName, out provided);
        }

        private static bool TryGetHeader(IHeaderDictionary headers, string name, out string value)
        {
            value = string.Empty;

            if (!headers.TryGetValue(name, out StringValues values))
                return false;

            var provided = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(provided))
                return false;

            value = provided;
            return true;
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
