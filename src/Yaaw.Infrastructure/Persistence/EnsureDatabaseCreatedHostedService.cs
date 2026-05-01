using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Yaaw.Infrastructure.Persistence;

internal sealed class EnsureDatabaseCreatedHostedService(IServiceProvider serviceProvider) : BackgroundService
{
    private static readonly string[] Roles = ["Admin", "User"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var scope = serviceProvider.CreateScope();

        // EnsureCreatedAsync only creates tables when the database doesn't exist yet.
        // With two DbContexts sharing the same database, the first call creates the DB
        // and its tables, but the second call sees the DB already exists and does nothing.
        // We use CreateTablesAsync on the second context to explicitly create its tables.

        using var appDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await appDbContext.Database.EnsureCreatedAsync(stoppingToken);

        using var identityDbContext = scope.ServiceProvider.GetRequiredService<IdentityAppDbContext>();
        var identityCreator = identityDbContext.GetService<IRelationalDatabaseCreator>();

        try
        {
            await identityCreator.CreateTablesAsync(stoppingToken);
        }
        catch (Exception)
        {
            // Tables already exist from a previous run — safe to ignore.
        }

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
