using Scalar.AspNetCore;
using Yaaw.API;
using Yaaw.API.Database;
using Yaaw.API.Endpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("yaaw");

builder.AddChatClient("llm");
builder.AddRedisClient("cache");

builder.Services.AddApiServices();

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapChatApi();

app.UseHttpsRedirection();

app.UseExceptionHandler();

await app.RunAsync();
