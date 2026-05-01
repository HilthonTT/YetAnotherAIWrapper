using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Yaaw.Application.Interfaces;
using Yaaw.Domain.Interfaces;
using Yaaw.Infrastructure.AI;
using Yaaw.Infrastructure.Caching;
using Yaaw.Infrastructure.Identity;
using Yaaw.Infrastructure.Persistence;
using Yaaw.Infrastructure.Persistence.Repositories;
using Yaaw.Infrastructure.Settings;

namespace Yaaw.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<ApplicationDbContext>("yaaw");
        builder.AddNpgsqlDbContext<IdentityAppDbContext>("yaaw");
        builder.AddRedisClient("cache");

        builder.Services.AddAuthServices(builder.Configuration);
        builder.Services.AddInfrastructureServices();

        return builder;
    }

    private static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        JwtOptions jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<IdentityAppDbContext>();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ClockSkew = TimeSpan.Zero,
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        string? accessToken = context.Request.Query["access_token"];

                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/api/chat/stream"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IIdentityService, IdentityService>();

        return services;
    }

    private static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddSingleton<RedisConversationState>();
        services.AddSingleton<RedisCancellationManager>();
        services.AddSingleton<IChatStreamingCoordinator, ChatStreamingCoordinator>();
        services.AddSingleton<ICancellationManager>(sp => sp.GetRequiredService<RedisCancellationManager>());

        services.AddHostedService<EnsureDatabaseCreatedHostedService>();
        services.AddHostedService<RedisConversationStateHostedService>();

        services.AddSingleton<ICacheKeyManager, CacheKeyManager>();
        services.AddScoped<IRedisCacheService, RedisCacheService>();

        return services;
    }
}
