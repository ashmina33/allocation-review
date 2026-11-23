using AllocationReview.Api.Data;
using AllocationReview.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AllocationReview.Api.Features.Runs;

public interface IRunQueryService
{
    Task<PagedResult<RunSummaryDto>> GetHistoryAsync(int page, int pageSize, CancellationToken ct);
    Task<RunReviewDto?> GetReviewAsync(string runCode, bool exceptionsOnly, CancellationToken ct);
}

/// <summary>Read-only queries for the review screens. Controllers stay thin; rules live in GapCalculator.</summary>
public sealed class RunQueryService(AppDbContext db) : IRunQueryService
{
    public async Task<PagedResult<RunSummaryDto>> GetHistoryAsync(int page, int pageSize, CancellationToken ct)
    {
        var total = await db.Runs.CountAsync(ct);

        // Newest first; Id is the tie-breaker so paging is stable when two runs share a date.
        // The total is aggregated in the database, so only one page of rows is ever loaded.
        var items = await db.Runs
            .AsNoTracking()
            .OrderByDescending(r => r.RunDate)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RunSummaryDto(
                r.RunCode,
                r.RunDate,
                r.Gaps.SelectMany(g => g.Allocations).Sum(a => (int?)a.Quantity) ?? 0))
            .ToListAsync(ct);

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResult<RunSummaryDto>(items, page, pageSize, total, totalPages);
    }

    public async Task<RunReviewDto?> GetReviewAsync(string runCode, bool exceptionsOnly, CancellationToken ct)
    {
        var run = await db.Runs
            .AsNoTracking()
            .Include(r => r.Gaps)
            .ThenInclude(g => g.Allocations)
            .SingleOrDefaultAsync(r => r.RunCode == runCode, ct);

        if (run is null) return null;

        // Each gap is one entity, so its shortfall is counted exactly once no matter how many batch rows it has.
        var rows = run.Gaps
            .OrderBy(g => g.GapCode)
            .Select(g =>
            {
                var result = GapCalculator.Calculate(g.Shortfall, g.Allocations.Select(a => a.Quantity));
                return new GapRowDto(g.GapCode, g.Store, g.Brand, g.ProductType,
                    result.OriginalShortfall, result.UnitsAllocated, result.UnitsRemaining, result.Status);
            })
            .ToList();

        var counts = new StatusCountsDto(
            rows.Count(r => r.Status == GapStatus.Filled),
            rows.Count(r => r.Status == GapStatus.PartiallyFilled),
            rows.Count(r => r.Status == GapStatus.Unfilled));

        // Summary numbers always describe the whole run; the filter only narrows the rows returned.
        var visible = exceptionsOnly ? rows.Where(r => GapCalculator.IsException(r.Status)).ToList() : rows;

        return new RunReviewDto(
            run.RunCode,
            run.RunDate,
            rows.Sum(r => r.UnitsAllocated),
            rows.Count,
            counts,
            exceptionsOnly,
            visible);
    }
}
