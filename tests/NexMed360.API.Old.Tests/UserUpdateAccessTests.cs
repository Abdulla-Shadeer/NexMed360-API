using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Dtos.User;
using NexMed360Api.Entities;
using NexMed360Api.Services;

namespace NexMed360.API.Old.Tests;

public class UserUpdateAccessTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingCallerIdentityIsRejectedBeforeAccessingAnyDependencies(string? requesterUserId)
    {
        // A query or mutation before authorization would fail: there is no provider.
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        var service = new UserService(new IdentityGateway(null!, null!, null!, context, null!), null!, TestViews.For(context));

        var result = await service.UpdateUser(
            new UpdateUserDto { Id = "target-user", Email = "target@example.test" }, requesterUserId);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCode.SYS_Unauthorized, result.Error!.Code);
        Assert.Equal(401, result.Error.HttpStatus);
    }

    [Theory]
    [InlineData("other-user")]
    [InlineData("OWNER-USER")]
    [InlineData("")]
    [InlineData(null)]
    public async Task DifferentTargetIdIsRejectedBeforeAccessingAnyDependencies(string? targetId)
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        var service = new UserService(new IdentityGateway(null!, null!, null!, context, null!), null!, TestViews.For(context));

        var result = await service.UpdateUser(
            new UpdateUserDto { Id = targetId!, Email = "already-used@example.test" }, "owner-user");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCode.SYS_Unauthorized, result.Error!.Code);
        Assert.Equal(403, result.Error.HttpStatus);
    }

    [Fact]
    public async Task OwnAccountCanBeUpdatedWithExistingStringIdAndResponseShape()
    {
        using var fixture = new Fixture();

        var result = await fixture.Service.UpdateUser(new UpdateUserDto
        {
            Id = "owner-user", FirstName = "Updated", LastName = "Name", PhoneNumber = "2222222222"
        }, "owner-user");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("owner-user", result.Data.Id);
        Assert.Equal("Updated", result.Data.FirstName);
        Assert.Equal("Name", result.Data.LastName);
        Assert.Equal("2222222222", result.Data.PhoneNumber);
        Assert.Contains(UserRoles.PLATFORM_ADMIN, result.Data.Roles);

        fixture.Context.ChangeTracker.Clear();
        var owner = await fixture.Context.Users.SingleAsync(user => user.Id == "owner-user");
        var other = await fixture.Context.Users.SingleAsync(user => user.Id == "other-user");
        Assert.Equal("Updated", owner.FirstName);
        Assert.Equal("owner@example.test", owner.Email);
        Assert.Equal("Other", other.FirstName);
        Assert.Equal("other@example.test", other.Email);
    }

    [Fact]
    public async Task EvenSuperuserCannotUseSelfServiceRouteToModifyAnotherAccount()
    {
        using var fixture = new Fixture();

        var result = await fixture.Service.UpdateUser(new UpdateUserDto
        {
            Id = "other-user", FirstName = "Changed", LastName = "Changed",
            Email = "other@example.test", PhoneNumber = "2222222222"
        }, "owner-user");

        Assert.False(result.IsSuccess);
        // Authorization takes precedence over the duplicate-email lookup.
        Assert.Equal(403, result.Error!.HttpStatus);
        Assert.Null(result.Data);
        fixture.Context.ChangeTracker.Clear();
        var owner = await fixture.Context.Users.SingleAsync(user => user.Id == "owner-user");
        var other = await fixture.Context.Users.SingleAsync(user => user.Id == "other-user");
        Assert.Equal("Owner", owner.FirstName);
        Assert.Equal("Other", other.FirstName);
        Assert.Equal("Patient", other.LastName);
        Assert.Equal("other@example.test", other.Email);
        Assert.Equal("1111111111", other.PhoneNumber);
    }

    [Fact]
    public async Task MissingOwnAccountKeepsExistingNotFoundResponse()
    {
        using var fixture = new Fixture();

        var result = await fixture.Service.UpdateUser(
            new UpdateUserDto { Id = "missing-user", FirstName = "Updated" }, "missing-user");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCode.USR_NotFound, result.Error!.Code);
        Assert.Equal(404, result.Error.HttpStatus);
    }

    [Fact]
    public async Task OwnUpdateKeepsExistingDuplicateEmailResponse()
    {
        using var fixture = new Fixture();

        var result = await fixture.Service.UpdateUser(
            new UpdateUserDto { Id = "owner-user", Email = "other@example.test" }, "owner-user");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.SYS_DuplicateEntry, result.Error!.Code);
        Assert.Equal(409, result.Error.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal("owner@example.test",
            (await fixture.Context.Users.SingleAsync(user => user.Id == "owner-user")).Email);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        public ApplicationDbContext Context { get; }
        public UserService Service { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
            Context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Context.Users.AddRange(ApplicationUser("owner-user", "Owner", "owner@example.test"),
                ApplicationUser("other-user", "Other", "other@example.test"));
            Context.Roles.Add(new IdentityRole(UserRoles.PLATFORM_ADMIN)
            {
                Id = "superuser-role", NormalizedName = UserRoles.PLATFORM_ADMIN.ToUpperInvariant()
            });
            Context.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = "owner-user", RoleId = "superuser-role"
            });
            TestProfiles.Save(Context);
            Context.ChangeTracker.Clear();
            Service = new UserService(new IdentityGateway(
                _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                NullLogger<IdentityGateway>.Instance, Context, null!), null!, TestViews.For(Context));
        }

        private static ApplicationUser ApplicationUser(string id, string firstName, string email) => new()
        {
            Id = id, FirstName = firstName, LastName = "Patient", Email = email,
            NormalizedEmail = email.ToUpperInvariant(), UserName = email,
            NormalizedUserName = email.ToUpperInvariant(), PhoneNumber = "1111111111"
        };

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }
}
