namespace AllocationReview.Api.Domain;

public enum GapStatus
{
    Filled,
    PartiallyFilled,
    Unfilled
}

public sealed record GapResult(int OriginalShortfall, int UnitsAllocated, int UnitsRemaining, GapStatus Status);

/// <summary>
/// Pure business rule for one gap. Kept free of EF/HTTP so it can be unit tested directly.
///
/// Rules (from the brief):
///  - count the original shortfall once, rounded UP to a whole unit;
///  - sum allocated units across all batch rows of the gap;
///  - remaining = shortfall - allocated, never below zero;
///  - Filled when 0 remain, Unfilled when units remain and nothing was allocated, otherwise PartiallyFilled.
/// </summary>
public static class GapCalculator
{
    public static GapResult Calculate(decimal shortfall, IEnumerable<int> batchQuantities)
    {
        ArgumentNullException.ThrowIfNull(batchQuantities);

        var original = (int)Math.Ceiling(Math.Max(0m, shortfall));
        var allocated = batchQuantities.Sum();
        var remaining = Math.Max(0, original - allocated);

        var status = remaining == 0
            ? GapStatus.Filled
            : allocated <= 0
                ? GapStatus.Unfilled
                : GapStatus.PartiallyFilled;

        return new GapResult(original, allocated, remaining, status);
    }

    public static bool IsException(GapStatus status) =>
        status is GapStatus.PartiallyFilled or GapStatus.Unfilled;
}
