using System.Net;
using System.Net.Http.Json;
using AllocationReview.Api.Features.Runs;

namespace AllocationReview.Api.Tests;

public class RunHistoryTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task History_is_newest_first_with_totals()
    {
        var client = await app.AdminAsync();
        var page = await client.GetFromJsonAsync<PagedResult<RunSummaryDto>>("/api/runs", RunReviewTests.Json);

        Assert.Equal(2, page!.TotalCount);
        Assert.Collection(page.Items,
            r => { Assert.Equal("R-102", r.RunId); Assert.Equal(new DateOnly(2026, 9, 14), r.RunDate); Assert.Equal(5, r.TotalAllocatedUnits); },
            r => { Assert.Equal("R-101", r.RunId); Assert.Equal(1, r.TotalAllocatedUnits); });
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    [InlineData("page=abc")]
    public async Task Invalid_pagination_returns_400(string query)
    {
        var client = await app.AdminAsync();
        var response = await client.GetAsync($"/api/runs?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class RunHistoryPagingTests(ManyRunsTestApp app) : IClassFixture<ManyRunsTestApp>
{
    [Fact]
    public async Task Pages_are_bounded_complete_and_non_overlapping()
    {
        var client = await app.AdminAsync();
        var seen = new List<string>();

        for (var p = 1; p <= 3; p++)
        {
            var page = await client.GetFromJsonAsync<PagedResult<RunSummaryDto>>(
                $"/api/runs?page={p}&pageSize=10", RunReviewTests.Json);

            Assert.Equal(25, page!.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.True(page.Items.Count <= 10);
            seen.AddRange(page.Items.Select(i => i.RunId));
        }

        Assert.Equal(25, seen.Count);
        Assert.Equal(25, seen.Distinct().Count()); // stable sort: no run repeated or skipped across pages
        Assert.Equal(new[] { "R-102", "R-101" }, seen.Take(2));
    }

    [Fact]
    public async Task Ordering_is_stable_between_identical_requests()
    {
        var client = await app.AdminAsync();
        var first = await client.GetFromJsonAsync<PagedResult<RunSummaryDto>>("/api/runs?page=2&pageSize=10", RunReviewTests.Json);
        var second = await client.GetFromJsonAsync<PagedResult<RunSummaryDto>>("/api/runs?page=2&pageSize=10", RunReviewTests.Json);

        Assert.Equal(first!.Items.Select(i => i.RunId), second!.Items.Select(i => i.RunId));
    }

    [Fact]
    public async Task Page_past_the_end_is_empty_not_an_error()
    {
        var client = await app.AdminAsync();
        var page = await client.GetFromJsonAsync<PagedResult<RunSummaryDto>>("/api/runs?page=99&pageSize=10", RunReviewTests.Json);
        Assert.Empty(page!.Items);
    }
}
