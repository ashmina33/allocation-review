using AllocationReview.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AllocationReview.Api.Features.Runs;

/// <summary>Read-only allocation review endpoints. Admin only, enforced on the server.</summary>
[ApiController]
[Route("api/runs")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class RunsController(IRunQueryService runs, ILogger<RunsController> logger) : ControllerBase
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    /// <summary>Saved runs, newest first, paginated on the server.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<RunSummaryDto>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        if (page < 1)
            ModelState.AddModelError(nameof(page), "page must be 1 or greater.");
        if (pageSize < 1 || pageSize > MaxPageSize)
            ModelState.AddModelError(nameof(pageSize), $"pageSize must be between 1 and {MaxPageSize}.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await runs.GetHistoryAsync(page, pageSize, ct));
    }

    /// <summary>One run with totals, counts by status and gap rows. exceptionsOnly=true returns partially filled and unfilled gaps.</summary>
    [HttpGet("{runId}")]
    public async Task<ActionResult<RunReviewDto>> GetReview(
        string runId,
        [FromQuery] bool exceptionsOnly = false,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });

        if (runId.Length > 32)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid run identifier.");

        var review = await runs.GetReviewAsync(runId, exceptionsOnly, ct);
        if (review is null)
        {
            logger.LogInformation("Run not found");
            return Problem(statusCode: StatusCodes.Status404NotFound, title: $"Run '{runId}' was not found.");
        }

        return Ok(review);
    }
}

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public static readonly string AdminRole = Roles.Admin;
}
