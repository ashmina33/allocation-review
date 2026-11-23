using AllocationReview.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AllocationReview.Api.Data;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Viewer = "Viewer";
}

public sealed class SeedOptions
{
    public const string Section = "Seed";
    public bool Enabled { get; set; }
    public string AdminEmail { get; set; } = "admin@demo.local";
    public string AdminPassword { get; set; } = "";
    public string ViewerEmail { get; set; } = "viewer@demo.local";
    public string ViewerPassword { get; set; } = "";
}

/// <summary>Idempotent seed: roles, two demo users, and the two synthetic runs from the brief.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, SeedOptions options, ILogger logger)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var role in new[] { Roles.Admin, Roles.Viewer })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        await EnsureUserAsync(userManager, options.AdminEmail, options.AdminPassword, Roles.Admin, logger);
        await EnsureUserAsync(userManager, options.ViewerEmail, options.ViewerPassword, Roles.Viewer, logger);

        if (!await db.Runs.AnyAsync())
        {
            db.Runs.AddRange(SyntheticRuns());
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded synthetic allocation runs");
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<IdentityUser> userManager, string email, string password, string role, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed password for role {Role} is not configured; user not created", role);
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                // Log error codes only - never the password.
                logger.LogError("Could not create demo user for role {Role}: {Errors}",
                    role, string.Join(",", result.Errors.Select(e => e.Code)));
                return;
            }
            logger.LogInformation("Created demo user for role {Role}", role);
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
    }

    /// <summary>
    /// The example saved rows from the brief. "Batch None / 0 units" (R-102 G-03) is modelled
    /// as a gap with no batch allocation rows.
    /// </summary>
    public static IEnumerable<AllocationRun> SyntheticRuns() =>
    [
        new AllocationRun
        {
            RunCode = "R-101",
            RunDate = new DateOnly(2026, 9, 13),
            Gaps =
            [
                new AllocationGap
                {
                    GapCode = "G-01", Store = "Store D", Brand = "Brand Z", ProductType = "Frames", Shortfall = 1,
                    Allocations = [new BatchAllocation { BatchCode = "B-601", Quantity = 1 }]
                }
            ]
        },
        new AllocationRun
        {
            RunCode = "R-102",
            RunDate = new DateOnly(2026, 9, 14),
            Gaps =
            [
                new AllocationGap
                {
                    GapCode = "G-01", Store = "Store A", Brand = "Brand X", ProductType = "Frames", Shortfall = 5,
                    Allocations =
                    [
                        new BatchAllocation { BatchCode = "B-401", Quantity = 2 },
                        new BatchAllocation { BatchCode = "B-402", Quantity = 1 }
                    ]
                },
                new AllocationGap
                {
                    GapCode = "G-02", Store = "Store B", Brand = "Brand X", ProductType = "Frames", Shortfall = 2,
                    Allocations = [new BatchAllocation { BatchCode = "B-501", Quantity = 2 }]
                },
                new AllocationGap
                {
                    GapCode = "G-03", Store = "Store C", Brand = "Brand Y", ProductType = "Sunglasses", Shortfall = 4
                }
            ]
        }
    ];
}
