namespace AllocationReview.Api.Infrastructure;

public static class ConnectionStrings
{
    /// <summary>
    /// Accepts either an Npgsql key/value string ("Host=...;Database=...") or a URL
    /// ("postgres://user:pass@host:5432/db"), which is what hosts such as Render provide
    /// in DATABASE_URL.
    /// </summary>
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        return $"Host={uri.Host};Port={port};Database={database};Username={user};Password={password};" +
               "SSL Mode=Require;Trust Server Certificate=true";
    }
}
