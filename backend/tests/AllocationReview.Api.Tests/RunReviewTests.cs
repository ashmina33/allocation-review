using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AllocationReview.Api.Domain;
using AllocationReview.Api.Features.Runs;

namespace AllocationReview.Api.Tests;

/// <summary>The acceptance data from the brief, checked through the real HTTP endpoint.</summary>
public class RunReviewTests(TestApp app) : IClassFixture<TestApp>
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Latest_run_matches_expected_review()
    {
        var client = await app.AdminAsync();
        var review = await client.GetFromJsonAsync<RunReviewDto>("/api/runs/R-102", Json);

        Assert.NotNull(review);
        Assert.Equal(3, review.GapCount);
        Assert.Equal(5, review.TotalAllocatedUnits);
        Assert.Equal(new StatusCountsDto(Filled: 1, PartiallyFilled: 1, Unfilled: 1), review.StatusCounts);

        Assert.Collection(review.Gaps,
            g => Assert.Equal(("G-01", 5, 3, 2, GapStatus.PartiallyFilled), Row(g)),
            g => Assert.Equal(("G-02", 2, 2, 0, GapStatus.Filled), Row(g)),
            g => Assert.Equal(("G-03", 4, 0, 4, GapStatus.Unfilled), Row(g)));
    }

    [Fact]
    public async Task Exceptions_filter_narrows_rows_but_keeps_full_run_totals()
    {
        var client = await app.AdminAsync();
        var review = await client.GetFromJsonAsync<RunReviewDto>("/api/runs/R-102?exceptionsOnly=true", Json);

        Assert.Equal(new[] { "G-01", "G-03" }, review!.Gaps.Select(g => g.GapId));
        Assert.Equal(5, review.TotalAllocatedUnits);
        Assert.Equal(3, review.GapCount);
        Assert.Equal(new StatusCountsDto(1, 1, 1), review.StatusCounts);
    }

    [Fact]
    public async Task Same_gap_id_in_another_run_is_a_separate_gap()
    {
        var client = await app.AdminAsync();
        var review = await client.GetFromJsonAsync<RunReviewDto>("/api/runs/R-101", Json);

        var gap = Assert.Single(review!.Gaps);
        Assert.Equal(("G-01", 1, 1, 0, GapStatus.Filled), Row(gap));
        Assert.Equal("Store D", gap.Store);
    }

    private static (string, int, int, int, GapStatus) Row(GapRowDto g) =>
        (g.GapId, g.OriginalShortfall, g.UnitsAllocated, g.UnitsRemaining, g.Status);
}
