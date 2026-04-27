namespace Yaaw.API.Settings;

public sealed class CorsOptions
{
    public const string PolicyName = "YaawCorsPolicy";
    public const string SectionName = "Cors";

    public required string[] AllowedOrigins { get; init; }
}
