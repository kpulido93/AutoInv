using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Services
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var h = context.Response.Headers;

                h[HeaderNames.XContentTypeOptions] = "nosniff";
                h[HeaderNames.XFrameOptions] = "DENY";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";
                h["X-Permitted-Cross-Domain-Policies"] = "none";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                h["Cross-Origin-Embedder-Policy"] = "require-corp";
                h["Cross-Origin-Resource-Policy"] = "same-origin";

                // Cache: evita cachear HTML con info sensible
                if (context.Response.ContentType?.Contains("text/html") == true)
                {
                    h["Cache-Control"] = "no-store, max-age=0";
                    h["Pragma"] = "no-cache";
                    h["Expires"] = "0";
                }

                // CSP (cuando ya NO tengas <style> inline)
                h["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "img-src 'self' data:; " +
                    "script-src 'self'; " +
                    "style-src 'self'; " +
                    "object-src 'none'; " +
                    "frame-ancestors 'none'; " +
                    "base-uri 'self'; " +
                    "form-action 'self'; " +
                    "upgrade-insecure-requests";

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder) =>
            builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
