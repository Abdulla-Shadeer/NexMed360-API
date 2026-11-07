using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexMed360.Infrastructure;
using NexMed360Api.Data;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests;

public class Phase1SeedTests
{
    [Fact]
    public async Task SeedingTwiceKeepsOnePlatformAccountAndTwoDemoOrganizations()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused",
            ["Auth:Jwt:Issuer"] = "test", ["Auth:Jwt:Audience"] = "test",
            ["Auth:Jwt:Signing-Key"] = "synthetic-test-key-at-least-32-characters", ["Auth:Jwt:Validity"] = "1",
            ["Auth:PlatformAdminPassword"] = "Synthetic-Test-Password-123!"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddDataProtection();
        services.AddRouting();
        services.AddNexMed360Infrastructure(configuration);
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.AddSingleton(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var seed = scope.ServiceProvider.GetRequiredService<IDbInitializerService>();
        await seed.SeedData();
        await seed.SeedData();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await context.Users.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.People.ToListAsync(TestContext.Current.CancellationToken));
        Assert.True((await context.Users.SingleAsync(TestContext.Current.CancellationToken)).IsPlatformAdmin);
        Assert.Equal(2, await context.Organizations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.Locations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await context.AccessRoles.CountAsync(TestContext.Current.CancellationToken));
    }
}
