using Yaaw.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var model = builder.AddAIModel("llm");

if (OperatingSystem.IsMacOS())
{
    // Just use OpenAI on MacOS, running ollama does not work well via docker
    // see https://github.com/CommunityToolkit/Aspire/issues/608
    model.AsOpenAI("gpt-4.1");
}
else
{
    model.RunAsOllama("phi4", c =>
    {
        // Enable to enable GPU support (if your machine has a GPU)
        c.WithGPUSupport();
        c.WithLifetime(ContainerLifetime.Persistent);
    })
    .PublishAsOpenAI("gpt-4.1");
}

var db = builder.AddPostgres("database")
    .WithDataVolume(builder.ExecutionContext.IsPublishMode ? "pgvolume" : null)
    .WithPgAdmin()
    .AddDatabase("yaaw");

var cache = builder.AddRedis("cache")
    .WithRedisInsight();

var api = builder.AddProject<Projects.Yaaw_API>("yaaw-api")
    .WithSwaggerUI()
    .WithScalar()
    .WithReDoc()
    .WithReference(model)
    .WaitFor(model)
    .WithReference(db)
    .WaitFor(db)
    .WithReference(cache)
    .WaitFor(cache);

builder.AddViteApp("yaaw-web", "../Yaaw.Web")
    .WithPnpm()
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("API_URL", api.GetEndpoint("https"))
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
