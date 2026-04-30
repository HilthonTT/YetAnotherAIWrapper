using Microsoft.AspNetCore.Components.Authorization;
using Yaaw.Web.Components;
using Yaaw.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<TokenStorageService>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<JwtDelegatingHandler>();

builder.Services.AddHttpClient<ChatApiService>(client =>
{
    client.BaseAddress = new Uri("https+http://yaaw-api");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddHttpMessageHandler<JwtDelegatingHandler>()
.AddServiceDiscovery();

builder.Services.AddHttpClient<AuthApiService>(client =>
{
    client.BaseAddress = new Uri("https+http://yaaw-api");
})
.AddServiceDiscovery();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "BlazorServer";
})
.AddCookie("BlazorServer", options =>
{
    options.LoginPath = "/login";
});
builder.Services.AddAuthorization();

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

app.UseStatusCodePages();

await app.RunAsync();
