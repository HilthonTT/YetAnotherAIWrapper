namespace Yaaw.API.Middleware.RateLimiting;

public sealed class RateLimiterOptions
{
    public const string SectionName = "RateLimiter";

    public int Limit { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}