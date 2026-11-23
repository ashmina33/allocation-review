using System.Text.Json.Serialization;
using AllocationReview.Api.Data;
using AllocationReview.Api.Features.Runs;
using AllocationReview.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
// Development: readable console with scopes. Other environments: one JSON object per line,
// including scopes (CorrelationId, RunId) so a failed request can be traced end to end.
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddSimpleConsole(o => { o.IncludeScopes = true; o.SingleLine = true; });
else
    builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.UseUtcTimestamp = true; });

// ---------- Database ----------
// The "Testing" environment registers its own (SQLite in-memory) provider in the test project.
if (!builder.Environment.IsEnvironment("Testing"))
{
    var raw = new[] { builder.Configuration.GetConnectionString("Default"), builder.Configuration["DATABASE_URL"] }
                  .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
              ?? throw new InvalidOperationException(
                  "No database configured. Set ConnectionStrings__Default (or DATABASE_URL).");
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(ConnectionStrings.Normalize(raw)));
}

// ---------- Identity + cookie authentication ----------
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>();

var sessionMinutes = builder.Configuration.GetValue("Auth:SessionMinutes", 30);
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "allocation.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict; // SPA is served from the same origin; blocks cross-site requests (CSRF)
    o.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(sessionMinutes);
    o.SlidingExpiration = true;

    // An API must answer 401/403, not redirect to an HTML login page.
    o.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; },
        OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; }
    };
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

// ---------- API ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(o =>
{
    // Every error body carries the correlation ID so a user report can be matched to the logs.
    o.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(ctx.HttpContext);
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<IRunQueryService, RunQueryService>();
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(); // ProblemDetails bodies for bare 401/403/404 responses

app.UseDefaultFiles();
app.UseStaticFiles(); // production: the built Angular app is published into wwwroot

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Endpoint not found."));
app.MapFallbackToFile("index.html"); // client-side routes (e.g. /runs/R-102) load the SPA

// ---------- Database migration + demo seed (config-driven, off in production by default) ----------
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
    {
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        logger.LogInformation("Database migrations applied");
    }

    var seed = app.Configuration.GetSection(SeedOptions.Section).Get<SeedOptions>() ?? new SeedOptions();
    if (seed.Enabled)
        await DbSeeder.SeedAsync(scope.ServiceProvider, seed, logger);
}

app.Run();

// Exposes Program to WebApplicationFactory in the test project.
public partial class Program;
