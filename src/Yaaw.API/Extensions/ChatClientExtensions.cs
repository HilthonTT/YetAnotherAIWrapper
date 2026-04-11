using CommunityToolkit.Aspire.OllamaSharp;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace Yaaw.API.Extensions;

public static class ChatClientExtensions
{
    public static IHostApplicationBuilder AddChatClient(this IHostApplicationBuilder builder, string connectionName)
    {
        var cs = builder.Configuration.GetConnectionString(connectionName);

        if (!ChatClientConnectionInfo.TryParse(cs, out var connectionInfo))
        {
            throw new InvalidOperationException($"Invalid connection string: {cs}. Expected format: 'Endpoint=endpoint;AccessKey=your_access_key;Model=model_name;Provider=ollama/openai/azureopenai;'.");
        }

        _ = connectionInfo.Provider switch
        {
            ClientChatProvider.Ollama => builder.ConfigureOllamaClient(connectionName, connectionInfo),
            ClientChatProvider.OpenAI => builder.ConfigureOpenAIClient(connectionName, connectionInfo),
            _ => throw new NotSupportedException($"Unsupported provider: {connectionInfo.Provider}")
        };

        return builder;
    }

    private static ChatClientBuilder ConfigureOpenAIClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        ChatClientConnectionInfo connectionInfo)
    {
        return builder.AddOpenAIClient(connectionName, settings => settings.EnableSensitiveTelemetryData = true).AddChatClient();
    }

    private static ChatClientBuilder ConfigureOllamaClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        ChatClientConnectionInfo connectionInfo)
    {
        var ollamaBuilder = builder.AddOllamaApiClient(connectionName, settings =>
        {
            settings.SelectedModel = connectionInfo.SelectedModel;
            SetDisableTracing(settings, true);
        });

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
