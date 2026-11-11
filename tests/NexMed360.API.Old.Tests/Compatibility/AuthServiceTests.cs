using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Entities;
using NexMed360.Application.Access;
using NexMed360Api.Common;
using NexMed360Api.Services;
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

    [Theory]
    [InlineData("account@example.test")]
    [InlineData("  ACCOUNT@EXAMPLE.TEST  ")]
    public async Task RegistrationCannotAttachRolesToAnExistingEmail(string email)
    {
        using var fixture = new Fixture();
        var auth = fixture.Services.GetRequiredService<IAuthService>();
        var original = await auth.RegisterUser(Request(), UserRoles.PATIENT);
        var changed = Request(); changed.Email = email; changed.FirstName = "Ignored";

        var result = await auth.RegisterUser(changed, UserRoles.PRACTITIONER);

        Assert.Equal(409, result.Error!.HttpStatus);
        var manager = fixture.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var account = (await manager.FindByIdAsync(original.Data!.Id))!;
        Assert.Equal("Synthetic", account.FirstName);
        Assert.Equal(new[] { UserRoles.PATIENT }, await manager.GetRolesAsync(account));
        Assert.True(await manager.CheckPasswordAsync(account, Request().Password));
        Assert.Equal(1, await fixture.Services.GetRequiredService<ApplicationDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task PublicSignupCreatesOnlyPatientAccessAndNormalLoginStillWorks()
    {
        using var fixture = new Fixture();
        var patients = fixture.Services.GetRequiredService<IPatientService>();
        var result = await patients.RegisterPatient(Signup());
        Assert.True(result.IsSuccess);
        var context = fixture.Services.GetRequiredService<ApplicationDbContext>();
        var account = await context.Users.SingleAsync();
        Assert.False(account.EmailConfirmed);
        Assert.Single(await context.People.ToListAsync());
        Assert.Equal(account.PersonId, (await context.Patients.SingleAsync()).PersonId);
        Assert.Empty(await context.OrganizationPatients.ToListAsync());
        Assert.Empty(await context.OrganizationMembers.ToListAsync());
        Assert.Empty(await context.Practitioners.ToListAsync());
        Assert.Empty(await context.MemberRoleAssignments.ToListAsync());
        var login = await fixture.Services.GetRequiredService<IAuthService>().LoginAsync(new() { Email = Request().Email, Password = Request().Password });
        Assert.True(login.IsSuccess);
        var workspace = Assert.Single(await fixture.Services.GetRequiredService<WorkspaceService>().List(account.Id));
        Assert.Equal(WorkspaceModes.Patient, workspace.Mode);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("confirmation")]
    [InlineData("birth-date")]
    [InlineData("gender")]
    [InlineData("password")]
    public async Task InvalidSignupCreatesNoAccountOrProfile(string invalid)
    {
        using var fixture = new Fixture();
        var request = Signup();
        if (invalid == "email") request.PersonalInformation.Email = "not-an-email";
        if (invalid == "confirmation") request.ConfirmPassword = "different";
        if (invalid == "birth-date") request.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        if (invalid == "gender") request.Gender = "invalid";
        if (invalid == "password") request.PersonalInformation.Password = request.ConfirmPassword = "weak";
        Assert.Equal(400, (await fixture.Services.GetRequiredService<IPatientService>().RegisterPatient(request)).Error!.HttpStatus);
        var context = fixture.Services.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await context.Users.ToListAsync());
        Assert.Empty(await context.People.ToListAsync());
        Assert.Empty(await context.Patients.ToListAsync());
    }

    [Fact]
    public async Task SignupCannotClaimAnExistingStaffAccount()
    {
        using var fixture = new Fixture();
        var auth = fixture.Services.GetRequiredService<IAuthService>();
        var staff = await auth.RegisterUser(Request(), UserRoles.PRACTITIONER);
        Assert.Equal(409, (await fixture.Services.GetRequiredService<IPatientService>().RegisterPatient(Signup())).Error!.HttpStatus);
        var context = fixture.Services.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await context.Patients.ToListAsync());
        Assert.Single(await context.Users.ToListAsync());
        var account = await fixture.Services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(staff.Data!.Id);
        Assert.Equal(new[] { UserRoles.PRACTITIONER }, await fixture.Services.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(account!));
    }

    [Fact]
    public async Task SignedInStaffCanEnablePatientAccessOnceWithoutChangingTheirWorkAccess()
    {
        using var fixture = new Fixture();
        var account = (await fixture.Services.GetRequiredService<IAuthService>().RegisterUser(Request(), UserRoles.PRACTITIONER)).Data!;
        var context = fixture.Services.GetRequiredService<ApplicationDbContext>();
        var organization = new Organization { Name = "Clinic" };
        var member = new OrganizationMember { UserId = account.Id, OrganizationId = organization.Id };
        var role = new AccessRole { OrganizationId = organization.Id, Name = "Nursing", Permissions = [Permissions.PatientsRead] };
        context.AddRange(organization, member, role, new MemberRoleAssignment { OrganizationId = organization.Id, OrganizationMemberId = member.Id, AccessRoleId = role.Id });
        await context.SaveChangesAsync();
        var patients = fixture.Services.GetRequiredService<IPatientService>();
        var result = await patients.EnablePatientAccess(new() { BirthDate = new(1995, 1, 2), Gender = "Unknown" }, account.Id);
        var repeated = await patients.EnablePatientAccess(new(), account.Id);
        Assert.True(result.IsSuccess);
        Assert.Equal(result.Data!.Id, repeated.Data!.Id);
        Assert.Single(await context.Patients.ToListAsync());
        Assert.Single(await context.MemberRoleAssignments.ToListAsync());
        Assert.Equal(account.PersonId, (await context.Patients.SingleAsync()).PersonId.ToString());
        Assert.Empty(await context.OrganizationPatients.ToListAsync());
        var manager = fixture.Services.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await manager.CheckPasswordAsync((await manager.FindByIdAsync(account.Id))!, Request().Password));
        var workspaces = await fixture.Services.GetRequiredService<WorkspaceService>().List(account.Id);
        Assert.Contains(workspaces, row => row.Mode == WorkspaceModes.Work);
        Assert.Contains(workspaces, row => row.Mode == WorkspaceModes.Patient);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing-account")]
    public async Task PatientEnablementRejectsMissingIdentity(string? userId)
    {
        using var fixture = new Fixture();
        Assert.Equal(401, (await fixture.Services.GetRequiredService<IPatientService>().EnablePatientAccess(new(), userId)).Error!.HttpStatus);
    }

    [Fact]
    public async Task EnablementDoesNotClaimAnotherPatientsMatchingEmail()
    {
        using var fixture = new Fixture();
        var account = (await fixture.Services.GetRequiredService<IAuthService>().RegisterUser(Request(), UserRoles.PRACTITIONER)).Data!;
        var context = fixture.Services.GetRequiredService<ApplicationDbContext>();
        var otherPerson = new Person { FirstName = "Someone", LastName = "Else", Email = Request().Email };
        var otherPatient = new Patient { PersonId = otherPerson.Id, BirthDate = new(1970, 1, 1), Gender = "Female" };
        context.AddRange(otherPerson, otherPatient); await context.SaveChangesAsync();
        var result = await fixture.Services.GetRequiredService<IPatientService>().EnablePatientAccess(new() { BirthDate = new(1995, 1, 2), Gender = "Male" }, account.Id);
        Assert.True(result.IsSuccess);
        Assert.NotEqual(otherPatient.Id.ToString(), result.Data!.Id);
        Assert.Equal(account.PersonId, result.Data.PersonId);
        Assert.Equal(2, await context.Patients.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingStaffAccountNeedsAnEstablishedMembershipForAnotherAssignment(bool established)
    {
        using var fixture = new Fixture();
        var account = (await fixture.Services.GetRequiredService<IAuthService>().RegisterUser(Request(), UserRoles.PRACTITIONER)).Data!;
        var context = fixture.Services.GetRequiredService<ApplicationDbContext>();
        var organization = new Organization { Name = "Clinic" };
        context.Add(organization);
        if (established) context.Add(new OrganizationMember { OrganizationId = organization.Id, UserId = account.Id });
        await context.SaveChangesAsync();
        var result = await fixture.Services.GetRequiredService<IOrganizationService>().AddPractitionerRole(new()
        {
            OrganizationId = organization.Id.ToString(), PersonalInformation = Request(), RoleName = "Nurse"
        });
        Assert.Equal(established, result.IsSuccess);
        Assert.Equal(established ? 1 : 0, await context.PractitionerRoles.CountAsync());
        if (!established) Assert.Equal(409, result.Error!.HttpStatus);
    }

    [Fact]
    public void SignupWithoutAnOptionalPhoneNumberPassesMvcValidation()
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var action = new Microsoft.AspNetCore.Mvc.ActionContext(
            new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = provider },
            new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var request = Signup(); request.PersonalInformation.PhoneNumber = null;
        provider.GetRequiredService<Microsoft.AspNetCore.Mvc.ModelBinding.Validation.IObjectModelValidator>()
            .Validate(action, null, "", request);
        Assert.True(action.ModelState.IsValid);
    }

    private static RegisterPatientDto Signup() => new()
    {
        PersonalInformation = Request(), ConfirmPassword = Request().Password,
        BirthDate = new(2000, 1, 1), Gender = "Unknown"
    };

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
            services.AddSingleton(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            scope = provider.CreateScope();
            var roles = Services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { UserRoles.PATIENT, UserRoles.PRACTITIONER })
                Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);
        }
        public void Dispose() { scope.Dispose(); provider.Dispose(); }
    }
}
