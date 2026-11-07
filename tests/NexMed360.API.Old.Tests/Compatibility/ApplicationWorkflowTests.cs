using NexMed360.Application.Accounts.Internal;
using NexMed360.Application.Shared;
using NexMed360Api.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexMed360.Infrastructure;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Dtos.Overview;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;
using NexMed360Api.Services;

namespace NexMed360.API.Old.Tests.Compatibility;

public class ApplicationWorkflowTests
{
    [Fact]
    public void MigratedServicesResolveWithoutStartingAHostOrConnectingToADatabase()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused",
            ["Auth:Jwt:Issuer"] = "test", ["Auth:Jwt:Audience"] = "test",
            ["Auth:Jwt:Signing-Key"] = "synthetic-test-key-at-least-32-characters", ["Auth:Jwt:Validity"] = "1"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddDataProtection();
        services.AddRouting();
        services.AddNexMed360Infrastructure(config);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        foreach (var type in new[] { typeof(IAuthService), typeof(IUserService), typeof(IPatientService), typeof(IOrganizationService),
                     typeof(IAppointmentService), typeof(IPractitionerScheduleService), typeof(ISearchService), typeof(IOverviewService),
                     typeof(IDbInitializerService), typeof(IRegistrationTransactions), typeof(IIdentityGateway) })
            Assert.NotNull(scope.ServiceProvider.GetRequiredService(type));
    }

    [Fact]
    public async Task LoginReturnsTheSameTokenAndUserProfile()
    {
        var users = new Users();
        var result = await new AuthService(new Identity(), users, TestViews.Empty()).LoginAsync(new LoginUserDto());
        Assert.True(result.IsSuccess);
        Assert.Equal("synthetic-token", result.Data!.AccessToken);
        Assert.Same(users.ApplicationUser, result.Data.User);
        Assert.Equal(users.ApplicationUser.Id, users.RequestedUserId);
    }

    [Fact]
    public async Task FailedLoginDoesNotLoadAProfile()
    {
        var users = new Users();
        var result = await new AuthService(new Identity { Reject = true }, users, TestViews.Empty()).LoginAsync(new LoginUserDto());
        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.Error!.HttpStatus);
        Assert.Null(users.RequestedUserId);
    }

    [Fact]
    public async Task PatientRegistrationUsesTheIdentityIdAndCommitsBothWrites()
    {
        var auth = new Registration(); var data = new PatientProfiles(); var transactions = new Transactions();
        var result = await new PatientService(auth, data, transactions, TestViews.Empty(), null!).CreatePatient(new CreatePatientDto());
        Assert.True(result.IsSuccess);
        Assert.Equal(UserRoles.PATIENT, auth.RegisteredRole);
        Assert.Equal(auth.ApplicationUser.Id, data.RegisteredUserId);
        Assert.Equal(1, transactions.Begun);
        Assert.True(transactions.Committed);
        Assert.True(transactions.Disposed);
    }

    [Theory]
    [InlineData(true)]
    public async Task RegistrationFailuresDisposeWithoutCommitting(bool accountFailure)
    {
        var auth = new Registration { RejectRegistration = accountFailure };
        var data = new PatientProfiles(); var transactions = new Transactions();
        var result = await new PatientService(auth, data, transactions, TestViews.Empty(), null!).CreatePatient(new CreatePatientDto());
        Assert.False(result.IsSuccess);
        if (accountFailure) Assert.Null(data.RegisteredUserId);
        Assert.False(transactions.Committed);
        Assert.True(transactions.Disposed);
    }

    [Fact]
    public async Task ProfileExceptionDisposesTheUncommittedRegistrationTransaction()
    {
        var transactions = new Transactions();
        var service = new PatientService(new Registration(), new PatientProfiles { Throw = true }, transactions, TestViews.Empty(), null!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePatient(new CreatePatientDto()));
        Assert.False(transactions.Committed);
        Assert.True(transactions.Disposed);
    }

    [Fact]
    public async Task MappingFailureDisposesTheUncommittedRegistrationTransaction()
    {
        var transactions = new Transactions();
        var service = new PatientService(new Registration(), new PatientProfiles(), transactions, new PortalViews(new FailedReads()), null!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePatient(new CreatePatientDto()));
        Assert.False(transactions.Committed);
        Assert.True(transactions.Disposed);
    }
    private sealed class FailedReads : IPortalReadStore
    {
        public Task<PortalReadData> Load(PortalReadRequest request) => throw new InvalidOperationException("Synthetic read projection failure");
    }

    private sealed class Identity : IIdentityGateway
    {
        public bool Reject { get; init; }
        public Task<Result<AuthenticatedAccount>> AuthenticateAsync(string email, string password) => Task.FromResult(Reject
            ? Result<AuthenticatedAccount>.Failure(ErrorCode.SYS_Unauthorized)
            : Result<AuthenticatedAccount>.Success(new AuthenticatedAccount("existing-user-id", "synthetic-token")));
        public Task<Result<AccountSummary>> RegisterUser(AccountRegistration dto, string role) => throw new NotSupportedException();
        public Task<Result<AccountSummary>> GetUserAsync(string id) => throw new NotSupportedException();
        public Task<Result<AccountSummary>> UpdateUser(AccountChanges dto, string? caller) => throw new NotSupportedException();
    }
    private sealed class Registration : IAuthService
    {
        public ViewUserDto ApplicationUser { get; } = new() { Id = "existing-user-id", PersonId = TestProfiles.PersonId("existing-user-id").ToString() };
        public bool RejectRegistration { get; init; }
        public string? RegisteredRole { get; private set; }
        public Task<Result<ViewUserDto>> RegisterUser(RegisterUserDto dto, string role)
        {
            RegisteredRole = role;
            return Task.FromResult(RejectRegistration ? Result<ViewUserDto>.Failure(ErrorCode.SYS_BadRequest) : Result<ViewUserDto>.Success(ApplicationUser));
        }
        public Task<Result<LoginResponseDto>> LoginAsync(LoginUserDto dto) => throw new NotSupportedException();
    }
    private sealed class Users : IUserService
    {
        public ViewUserDto ApplicationUser { get; } = new() { Id = "existing-user-id", PersonId = TestProfiles.PersonId("existing-user-id").ToString() };
        public string? RequestedUserId { get; private set; }
        public Task<Result<ViewUserDto>> GetUserAsync(string id) { RequestedUserId = id; return Task.FromResult(Result<ViewUserDto>.Success(ApplicationUser)); }
        public Task<Result<List<OverviewDto>>> GetOverview(CallerContext caller) => throw new NotSupportedException();
        public Task<Result<ViewUserDto>> UpdateUser(UpdateUserDto dto, string? caller) => throw new NotSupportedException();
    }
    private sealed class PatientProfiles : IPatientRepository
    {
        public bool Throw { get; init; }
        public string? RegisteredUserId { get; private set; }
        public Task<Patient> Add(Patient patient)
        {
            RegisteredUserId = TestProfiles.UserId(patient);
            if (Throw) throw new InvalidOperationException("Synthetic profile failure");
            return Task.FromResult(patient);
        }
        public Task<Patient?> Find(Guid id) => throw new NotSupportedException();
        public Task<PractitionerRole?> FindPractitionerRole(Guid roleId, string userId) => throw new NotSupportedException();
        public Task<bool> PatientExists(Guid id) => throw new NotSupportedException();
        public Task<ClinicalRecord> AddRecord(ClinicalRecord record) => throw new NotSupportedException();
        public Task<ClinicalRecord?> FindReadableRecord(Guid id, string caller, bool practitionerRole) => throw new NotSupportedException();
        public Task<bool> CanReadPatient(Guid id, string caller, bool practitionerRole) => throw new NotSupportedException();
        public Task<List<ClinicalRecord>> GetRecords(Guid id, string caller, bool practitionerRole) => throw new NotSupportedException();
    }
    private sealed class Transactions : IRegistrationTransactions, IRegistrationTransaction
    {
        public int Begun { get; private set; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public Task<IRegistrationTransaction> BeginAsync() { Begun++; return Task.FromResult<IRegistrationTransaction>(this); }
        public Task CommitAsync() { Committed = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
