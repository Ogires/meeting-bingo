namespace MeetingBingo.Api.Security;

/// <summary>Security headers on every response, including error responses and the Angular files.</summary>
internal static class SecurityHeaders
{
    // Only what the CSP meta tag that Angular's autoCsp writes into index.html cannot set (frame-ancestors)
    // or does not set. No script-src or default-src here: both policies apply, so they would block
    // the inline loader script that autoCsp allows by its hash.
    internal const string ContentSecurityPolicy =
        "frame-ancestors 'none'; base-uri 'self'; object-src 'none'; form-action 'self'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            // Read now: the SPA fallback rewrites the path to /index.html before the response starts
            var isRoom = context.Request.Path.StartsWithSegments("/s");

            // Set when the response starts, so they survive the exception handler clearing the response
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

                // A room link is shared within a meeting: keep it out of search engines
                if (isRoom)
                    headers["X-Robots-Tag"] = "noindex";

                return Task.CompletedTask;
            });

            return next(context);
        });
}
