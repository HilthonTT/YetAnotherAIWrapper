using Scalar.AspNetCore;
using Yaaw.API;
using Yaaw.API.Database;
using Yaaw.API.Endpoints;
using Yaaw.API.Settings;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("yaaw");

builder.AddChatClient("llm");
builder.AddRedisClient("cache");

builder.Services
    .AddApiServices()
    .AddRateLimiting()
    .AddErrorHandling();

builder.AddCorsPolicy();

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "OpenAPI V1");
    });

    app.UseReDoc(options =>
    {
        options.SpecUrl("/openapi/v1.json");
    });
}

app.MapChatApi();

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseCors(CorsOptions.PolicyName);

app.UseRateLimiter();

app.UseStatusCodePages();

await app.RunAsync();
