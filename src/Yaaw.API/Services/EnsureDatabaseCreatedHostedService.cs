using Microsoft.AspNetCore.Identity;
using Yaaw.API.Database;

namespace Yaaw.API.Services;

internal sealed class EnsureDatabaseCreatedHostedService(IServiceProvider serviceProvider) : BackgroundService
{
    private static readonly string[] Roles = ["Admin", "User"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync(stoppingToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (string role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
