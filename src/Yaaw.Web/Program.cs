using Yaaw.Web.Components;
using Yaaw.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<ChatApiService>(client =>
{
    client.BaseAddress = new Uri("https+http://yaaw-api");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddServiceDiscovery();

builder.Services.AddScoped<ChatStreamService>();
builder.Services.AddSingleton<MarkdownService>();

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
