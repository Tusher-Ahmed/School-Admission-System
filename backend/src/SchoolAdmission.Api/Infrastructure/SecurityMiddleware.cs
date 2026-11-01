using Microsoft.AspNetCore.Http.Features;

namespace SchoolAdmission.Api.Infrastructure;

public static class SecurityMiddleware
{
    /// <summary>The largest real request is a class name of 200 characters; 16 KB leaves plenty of room.</summary>
    public const long MaxRequestBodyBytes = 16 * 1024;

    /// <summary>
    /// Adds security headers to every response and rejects oversized bodies early, so a client cannot make the
    /// server buffer megabytes of JSON (Kestrel's default limit is about 30 MB).
    /// </summary>
    public static IApplicationBuilder UseSecurityDefaults(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // OnStarting so the headers survive the exception handler, which clears headers on error.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                // Seat counts and hold states change every second; never serve them from a cache.
                headers["Cache-Control"] = "no-store";
                return Task.CompletedTask;
            });

            var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature is { IsReadOnly: false })
                sizeFeature.MaxRequestBodySize = MaxRequestBodyBytes; // also covers chunked bodies on Kestrel

            if (context.Request.ContentLength > MaxRequestBodyBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                var (code, message) = ApiErrors.Describe(StatusCodes.Status413PayloadTooLarge);
                await context.Response.WriteAsJsonAsync(new { error = code, message });
                return;
            }

            await next(context);
        });

    /// <summary>
    /// Browsers may call the API only from the local frontend (any port on localhost / 127.0.0.1, so Vite moving to
    /// 5174 still works) or from origins listed in CORS_ORIGINS (comma separated). Other websites get no CORS
    /// headers, so a malicious page open in the same browser cannot use the API.
    /// </summary>
    public static Func<string, bool> AllowedOrigins(IConfiguration config)
    {
        var extra = (config["CORS_ORIGINS"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return origin =>
        {
            if (extra.Contains(origin.TrimEnd('/'))) return true;
            return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && uri.Host is "localhost" or "127.0.0.1" or "[::1]";
        };
    }
}
