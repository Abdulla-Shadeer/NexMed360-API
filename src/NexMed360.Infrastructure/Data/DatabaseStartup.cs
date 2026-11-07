using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure;

public static class DatabaseStartup
{
    public static async Task InitializeNexMedDatabaseAsync(this IServiceProvider services, bool migrate, bool seed)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("NexMed360Api.Program");
        if (migrate)
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (context.Database.IsNpgsql())
            {
                logger.LogInformation("Applying database migrations...");
                await context.Database.MigrateAsync();
            }
        }
        if (seed)
        {
            logger.LogInformation("Seeding data...");
            await scope.ServiceProvider.GetRequiredService<IDbInitializerService>().SeedData();
        }
    }
}
