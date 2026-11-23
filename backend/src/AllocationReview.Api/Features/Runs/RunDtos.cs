using AllocationReview.Api.Domain;

namespace AllocationReview.Api.Features.Runs;

public sealed record RunSummaryDto(string RunId, DateOnly RunDate, int TotalAllocatedUnits);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record GapRowDto(
    string GapId,
    string Store,
    string Brand,
    string ProductType,
    int OriginalShortfall,
    int UnitsAllocated,
    int UnitsRemaining,
    GapStatus Status);

public sealed record StatusCountsDto(int Filled, int PartiallyFilled, int Unfilled);

public sealed record RunReviewDto(
    string RunId,
    DateOnly RunDate,
    int TotalAllocatedUnits,
    int GapCount,
    StatusCountsDto StatusCounts,
    bool ExceptionsOnly,
    IReadOnlyList<GapRowDto> Gaps);
