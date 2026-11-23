using System.Text.RegularExpressions;

namespace AllocationReview.Api.Infrastructure;

/// <summary>
/// Gives every request a correlation ID (accepts a safe incoming X-Correlation-ID, otherwise
/// generates one), echoes it in the response header, and adds it to the logging scope so every
/// log line for the request can be found together.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        // Only accept short, simple values so a client cannot inject arbitrary text into logs.
        var correlationId = SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);

            // One summary line per API request: method, path template-ish, status. No query string,
            // headers or body, so tokens and raw request data stay out of the logs.
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var status = context.Response.StatusCode;
                var level = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
                logger.Log(level, "HTTP {Method} {Path} responded {StatusCode}",
                    context.Request.Method, context.Request.Path.Value, status);
            }
        }
    }

    public static string? Get(HttpContext context) => context.Items[ItemKey] as string;

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex SafeId();
}
