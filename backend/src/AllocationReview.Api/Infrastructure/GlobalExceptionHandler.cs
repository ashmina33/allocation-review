using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AllocationReview.Api.Infrastructure;

/// <summary>
/// Turns unhandled exceptions into a controlled 500 ProblemDetails response.
/// The client gets a generic message plus the correlation ID; details stay in the server log.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            context.Request.Method, context.Request.Path.Value);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Please try again. If the problem continues, contact support with the correlation ID."
        };
        problem.Extensions["correlationId"] = CorrelationIdMiddleware.Get(context);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
