using AllocationReview.Api.Domain;

namespace AllocationReview.Api.Tests;

public class GapCalculatorTests
{
    [Fact]
    public void Multi_batch_gap_sums_batches_and_counts_shortfall_once()
    {
        // R-102 G-01: shortfall 5, batches B-401 (2) + B-402 (1)
        var result = GapCalculator.Calculate(5m, [2, 1]);

        Assert.Equal(new GapResult(5, 3, 2, GapStatus.PartiallyFilled), result);
    }

    [Fact]
    public void Fully_allocated_gap_is_filled()
    {
        Assert.Equal(new GapResult(2, 2, 0, GapStatus.Filled), GapCalculator.Calculate(2m, [2]));
    }

    [Fact]
    public void Gap_with_no_batches_is_unfilled()
    {
        Assert.Equal(new GapResult(4, 0, 4, GapStatus.Unfilled), GapCalculator.Calculate(4m, []));
    }

    [Fact]
    public void Over_allocation_never_produces_negative_remaining()
    {
        Assert.Equal(new GapResult(3, 5, 0, GapStatus.Filled), GapCalculator.Calculate(3m, [4, 1]));
    }

    [Theory]
    [InlineData(4.2, 5)]
    [InlineData(4.0, 4)]
    [InlineData(0.1, 1)]
    public void Shortfall_is_rounded_up_to_a_whole_unit(double shortfall, int expected)
    {
        Assert.Equal(expected, GapCalculator.Calculate((decimal)shortfall, []).OriginalShortfall);
    }

    [Fact]
    public void Zero_shortfall_is_filled_even_without_batches()
    {
        Assert.Equal(GapStatus.Filled, GapCalculator.Calculate(0m, []).Status);
    }

    [Theory]
    [InlineData(GapStatus.PartiallyFilled, true)]
    [InlineData(GapStatus.Unfilled, true)]
    [InlineData(GapStatus.Filled, false)]
    public void Exceptions_are_partially_filled_and_unfilled(GapStatus status, bool expected)
    {
        Assert.Equal(expected, GapCalculator.IsException(status));
    }
}
