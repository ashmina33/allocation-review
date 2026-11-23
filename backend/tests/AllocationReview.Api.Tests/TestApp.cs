using System.Net.Http.Json;
using AllocationReview.Api.Data;
using AllocationReview.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllocationReview.Api.Tests;

/// <summary>
/// Runs the real API pipeline (auth, controllers, middleware) against an in-memory SQLite database,
/// so tests need no PostgreSQL server, Docker, or client data.
/// </summary>
public class TestApp : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin#test123";
    public const string ViewerEmail = "viewer@test.local";
    public const string ViewerPassword = "Viewer#test123";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        _connection.Open(); // the in-memory database lives as long as this connection

        builder.ConfigureTestServices(services =>
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection)));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        DbSeeder.SeedAsync(scope.ServiceProvider, new SeedOptions
        {
            AdminEmail = AdminEmail,
            AdminPassword = AdminPassword,
            ViewerEmail = ViewerEmail,
            ViewerPassword = ViewerPassword
        }, NullLogger.Instance).GetAwaiter().GetResult();

        AdditionalSeed(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        return host;
    }

    protected virtual void AdditionalSeed(AppDbContext db) { }

    public async Task<HttpClient> SignedInClientAsync(string email, string password)
    {
        var client = CreateClient(); // keeps cookies between requests
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public Task<HttpClient> AdminAsync() => SignedInClientAsync(AdminEmail, AdminPassword);
    public Task<HttpClient> ViewerAsync() => SignedInClientAsync(ViewerEmail, ViewerPassword);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

/// <summary>Adds 23 extra runs (25 total), several sharing one date, to exercise paging and tie-breaks.</summary>
public sealed class ManyRunsTestApp : TestApp
{
    protected override void AdditionalSeed(AppDbContext db)
    {
        for (var i = 1; i <= 23; i++)
        {
            db.Runs.Add(new AllocationRun
            {
                RunCode = $"R-{200 + i}",
                RunDate = new DateOnly(2026, 8, 1).AddDays(i % 5), // repeated dates on purpose
                Gaps =
                [
                    new AllocationGap
                    {
                        GapCode = "G-01", Store = "Store T", Brand = "Brand T", ProductType = "Frames", Shortfall = 2,
                        Allocations = [new BatchAllocation { BatchCode = $"B-{900 + i}", Quantity = 1 }]
                    }
                ]
            });
        }
        db.SaveChanges();
    }
}
