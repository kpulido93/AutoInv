using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Webhook.Services
{
    /// <summary>
    /// Middleware simple para proteger rutas sensibles mediante una API Key.
    /// 
    /// Protege:
    ///  - /id-clients
    ///  - /updates (incluye latest.json y binarios en wwwroot)
    /// 
    /// Header requerido: X-AutoInventario-Key
    /// Valor: configurado en appsettings.json (AutoInventario:ApiKey) o variable de entorno.
    /// </summary>
    public sealed class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;

        private const string HeaderName = "X-AutoInventario-Key";

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _config = config;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            bool needsKey = path.Equals("/id-clients", StringComparison.OrdinalIgnoreCase);
                            //|| path.StartsWith("/updates", StringComparison.OrdinalIgnoreCase);

            if (!needsKey)
            {
                await _next(context);
                return;
            }

            var expected = _config["AutoInventario:ApiKey"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Server misconfigured: AutoInventario:ApiKey is not set.");
                return;
            }

            if (!context.Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing API key.");
                return;
            }

            // Comparación en tiempo constante (evita timing attacks triviales)
            if (!FixedTimeEquals(provided.ToString(), expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid API key.");
                return;
            }

            await _next(context);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
            var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
            return aBytes.Length == bBytes.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
