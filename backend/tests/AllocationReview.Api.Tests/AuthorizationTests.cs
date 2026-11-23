using System.Net;
using System.Net.Http.Json;

namespace AllocationReview.Api.Tests;

public class AuthorizationTests(TestApp app) : IClassFixture<TestApp>
{
    [Theory]
    [InlineData("/api/runs")]
    [InlineData("/api/runs/R-102")]
    public async Task Unauthenticated_requests_get_401(string url)
    {
        var response = await app.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/runs")]
    [InlineData("/api/runs/R-102")]
    public async Task Viewer_requests_get_403(string url)
    {
        var client = await app.ViewerAsync();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/runs")]
    [InlineData("/api/runs/R-102")]
    public async Task Admin_requests_succeed(string url)
    {
        var client = await app.AdminAsync();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_run_returns_404_to_admin()
    {
        var client = await app.AdminAsync();
        var response = await client.GetAsync("/api/runs/R-999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_with_401()
    {
        var response = await app.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = TestApp.AdminEmail, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_roles_for_signed_in_user()
    {
        var client = await app.ViewerAsync();
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.Equal(new[] { "Viewer" }, me!.Roles);
    }

    [Fact]
    public async Task Error_responses_carry_a_correlation_id()
    {
        var response = await app.CreateClient().GetAsync("/api/runs");
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    private sealed record MeResponse(string Email, string[] Roles);
}
