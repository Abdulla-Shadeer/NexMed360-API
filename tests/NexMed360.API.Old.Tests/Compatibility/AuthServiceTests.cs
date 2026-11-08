using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NexMed360.Infrastructure;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests.Compatibility;

public class AuthServiceTests
{
    [Fact]
    public async Task RegistrationAndLoginRetainAccountProfileAndValidatedTokenClaims()
    {
        using var fixture = new Fixture();
        var auth = fixture.Services.GetRequiredService<IAuthService>();
        var registered = await auth.RegisterUser(Request(), UserRoles.PATIENT);
        Assert.True(registered.IsSuccess);
        var profile = registered.Data!;
        Assert.False(string.IsNullOrWhiteSpace(profile.Id));
        Assert.Equal("Synthetic", profile.FirstName);
        Assert.Equal("Account", profile.LastName);
        Assert.Equal("account@example.test", profile.Email);
        Assert.Equal("0000000000", profile.PhoneNumber);
        Assert.True(Guid.TryParse(profile.PersonId, out _));

        var login = await auth.LoginAsync(new LoginUserDto { Email = profile.Email, Password = "SyntheticTest1!" });
        Assert.True(login.IsSuccess);
        Assert.Equal(profile.Id, login.Data!.User.Id);
        Assert.Equal(profile.Email, login.Data.User.Email);
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(login.Data.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = "synthetic-issuer",
            ValidateAudience = true, ValidAudience = "synthetic-audience",
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Fixture.SigningKey)),
            ClockSkew = TimeSpan.Zero
        }, out var validated);
        var token = Assert.IsType<JwtSecurityToken>(validated);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Equal(profile.Id, token.Subject);
        Assert.True(Guid.TryParse(token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sid).Value, out _));
        Assert.True(principal.IsInRole(UserRoles.PATIENT));
        Assert.Equal(profile.Id, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddHours(23), DateTime.UtcNow.AddHours(25));
    }

    [Theory]
    [InlineData("account@example.test", "WrongPassword1!")]
    [InlineData("unknown@example.test", "SyntheticTest1!")]
    public async Task InvalidCredentialsKeepTheExistingUnauthorizedResponse(string email, string password)
    {
        using var fixture = new Fixture();
        var auth = fixture.Services.GetRequiredService<IAuthService>();
        Assert.True((await auth.RegisterUser(Request(), UserRoles.PATIENT)).IsSuccess);
        var result = await auth.LoginAsync(new LoginUserDto { Email = email, Password = password });
        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.Error!.HttpStatus);
        Assert.Equal("Incorrect username or password!", result.Error.Description);
    }

    [Fact]
    public async Task AddingAnotherOrganizationAssignmentCanReuseTheExistingAccountRole()
    {
        using var fixture = new Fixture();
        var auth = fixture.Services.GetRequiredService<IAuthService>();
        var original = await auth.RegisterUser(Request(), UserRoles.PATIENT);
        Assert.True(original.IsSuccess);
        var changed = Request();
        changed.FirstName = "Ignored";
        var added = await auth.RegisterUser(changed, UserRoles.PRACTITIONER);
        Assert.True(added.IsSuccess);
        Assert.Equal(original.Data!.Id, added.Data!.Id);
        Assert.Equal("Synthetic", added.Data.FirstName);
        var manager = fixture.Services.GetRequiredService<UserManager<NexMed360Api.Entities.ApplicationUser>>();
        var account = await manager.FindByIdAsync(added.Data.Id);
        Assert.Equal(new[] { UserRoles.PRACTITIONER, UserRoles.PATIENT }.Order(), (await manager.GetRolesAsync(account!)).Order());
        Assert.Equal(1, await fixture.Services.GetRequiredService<ApplicationDbContext>().Users.CountAsync(TestContext.Current.CancellationToken));
        var duplicate = await auth.RegisterUser(Request(), UserRoles.PRACTITIONER);
        Assert.True(duplicate.IsSuccess);
        Assert.Equal(original.Data.Id, duplicate.Data!.Id);
        Assert.Equal(added.Data.PersonId, duplicate.Data.PersonId);
    }

    [Fact]
    public async Task RejectedRegistrationDoesNotCreateAnAccount()
    {
        using var fixture = new Fixture();
        var request = Request(); request.Password = "weak";
        var result = await fixture.Services.GetRequiredService<IAuthService>().RegisterUser(request, UserRoles.PATIENT);
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error!.HttpStatus);
        Assert.False(await fixture.Services.GetRequiredService<ApplicationDbContext>().Users.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdatingOwnEmailKeepsLoginLookupInSync()
    {
        using var fixture = new Fixture();
        var auth = fixture.Services.GetRequiredService<IAuthService>();
        var registered = await auth.RegisterUser(Request(), UserRoles.PATIENT);
        var users = fixture.Services.GetRequiredService<IUserService>();
        var id = registered.Data!.Id;
        var unchanged = await users.UpdateUser(new UpdateUserDto { Id = id, Email = registered.Data.Email }, id);
        Assert.True(unchanged.IsSuccess);
        var changed = await users.UpdateUser(new UpdateUserDto { Id = id, Email = "new-address@example.test" }, id);
        Assert.True(changed.IsSuccess);
        var login = await auth.LoginAsync(new LoginUserDto { Email = "new-address@example.test", Password = "SyntheticTest1!" });
        Assert.True(login.IsSuccess);
        Assert.Equal(id, login.Data!.User.Id);
        Assert.False((await auth.LoginAsync(new LoginUserDto { Email = "account@example.test", Password = "SyntheticTest1!" })).IsSuccess);
    }

    private static RegisterUserDto Request() => new()
    {
        FirstName = "Synthetic", LastName = "Account", Email = "account@example.test",
        PhoneNumber = "0000000000", Password = "SyntheticTest1!"
    };

    private sealed class Fixture : IDisposable
    {
        internal const string SigningKey = "synthetic-auth-regression-key-at-least-32-characters";
        private readonly ServiceProvider provider;
        private readonly IServiceScope scope;
        public IServiceProvider Services => scope.ServiceProvider;
        public Fixture()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused",
                ["Auth:Jwt:Issuer"] = "synthetic-issuer", ["Auth:Jwt:Audience"] = "synthetic-audience",
                ["Auth:Jwt:Signing-Key"] = SigningKey, ["Auth:Jwt:Validity"] = "1"
            }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging(); services.AddDataProtection(); services.AddRouting();
            services.AddNexMed360Infrastructure(config);
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddSingleton(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            scope = provider.CreateScope();
            var roles = Services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { UserRoles.PATIENT, UserRoles.PRACTITIONER })
                Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);
        }
        public void Dispose() { scope.Dispose(); provider.Dispose(); }
    }
}
