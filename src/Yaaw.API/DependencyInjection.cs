using CommunityToolkit.Aspire.OllamaSharp;
using FluentValidation;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using Yaaw.API.DTOs.Conversations;
using Yaaw.API.Entities;
using Yaaw.API.Extensions;
using Yaaw.API.Middleware;
using Yaaw.API.Services;
using Yaaw.API.Services.Sorting;
using Yaaw.API.Settings;

namespace Yaaw.API;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddChatClient(this WebApplicationBuilder builder, string connectionString)
    {
        string? cs = builder.Configuration.GetConnectionString(connectionString);

        if (!ChatClientConnectionInfo.TryParse(cs, out var connectionInfo))
        {
            throw new InvalidOperationException($"Invalid connection string: {cs}. Expected format: 'Endpoint=endpoint;AccessKey=your_access_key;Model=model_name;Provider=ollama/openai/azureopenai;'.");
        }

        _ = connectionInfo.Provider switch
        {
            ClientChatProvider.Ollama => builder.AddOllamaClient(connectionString, connectionInfo),
            ClientChatProvider.OpenAI => builder.AddOpenAIClient(connectionString, connectionInfo),
            _ => throw new NotSupportedException($"Unsupported provider: {connectionInfo.Provider}")
        };

        return builder;
    }
    
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
            };
        });
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Program>();

        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddOpenApi();
        services.AddSignalR();
        services.AddSingleton<ChatStreamingCoordinator>();
        services.AddSingleton<RedisConversationState>();
        services.AddSingleton<RedisCancellationManager>();

        services.AddHostedService<EnsureDatabaseCreatedHostedService>();
        services.AddHostedService<RedisConversationStateHostedService>();

        services.AddTransient<LinkService>();
        services.AddTransient<DataShapingService>();
        services.AddTransient<SortMappingProvider>();

        services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<Conversation, ConversationDto>>(
            _ => ConversationMappings.SortMappings);

        return services;
    }

    public static WebApplicationBuilder AddCorsPolicy(this WebApplicationBuilder builder)
    {
        CorsOptions corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()!;

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, policy =>
            {
                policy.WithOrigins(corsOptions.AllowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return builder;
    }

    private static ChatClientBuilder AddOpenAIClient(this IHostApplicationBuilder builder, string connectionName, ChatClientConnectionInfo connectionInfo)
    {
        return builder.AddOpenAIClient(connectionName, settings => settings.EnableSensitiveTelemetryData = true).AddChatClient();
    }

    private static ChatClientBuilder AddOllamaClient(this IHostApplicationBuilder builder, string connectionName, ChatClientConnectionInfo connectionInfo)
    {
        var ollamaBuilder = builder.AddOllamaApiClient(connectionName, settings =>
        {
            settings.SelectedModel = connectionInfo.SelectedModel;
            SetDisableTracing(settings, true);
        });

        // Set up OpenTelemetry for tracing and metrics. This needs to be default in the 
        // community toolkit.
        var telemetryName = "Experimental.Microsoft.Extensions.AI";

        builder.Services.AddOpenTelemetry()
               .WithTracing(t => t.AddSource(telemetryName))
               .WithMetrics(m => m.AddMeter(telemetryName));

        return ollamaBuilder.AddChatClient()
                    .UseOpenTelemetry(configure: options => options.EnableSensitiveData = true)
                    .UseLogging();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_DisableTracing")]
    public static extern void SetDisableTracing(OllamaSharpSettings settings, bool value);
}
