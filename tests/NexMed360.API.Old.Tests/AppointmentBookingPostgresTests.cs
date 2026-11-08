using NexMed360.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NexMed360.Infrastructure.Data;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;
using NexMed360Api.Common;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Common.Auth;

using NexMed360Api.Services;
using Npgsql;

namespace NexMed360.API.Old.Tests;

[Trait("Category", "PostgreSQL")]
public class AppointmentBookingPostgresTests
{
    public const string ConnectionVariable = "NEXMED360_REFACTORED_TEST_POSTGRES";
    private const string SkipReason = "Set NEXMED360_REFACTORED_TEST_POSTGRES for the separate nexmed360_refactored_tests database.";
    public static bool HasPostgresConfiguration =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasPostgresConfiguration))]
    public async Task CompetingBookingsProduceOneWinnerAndOneConflict()
    {
        var options = await PrepareDatabase();
        using var fixture = new AppointmentBookingEligibilityTests.Fixture(options);
        var barrier = new BothRequestsReady();
        var competingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options)
            .AddInterceptors(barrier).Options;
        await using var firstContext = new ApplicationDbContext(competingOptions);
        await using var secondContext = new ApplicationDbContext(competingOptions);

        var results = await Task.WhenAll(
            Service(firstContext).Create(fixture.Request(), fixture.RequesterId),
            Service(secondContext).Create(fixture.Request(), fixture.RequesterId));

        Assert.Equal(2, barrier.RequestCount);
        var winner = Assert.Single(results.Where(result => result.IsSuccess));
        Assert.Equal("Synthetic Patient", winner.Data!.PatientName);
        Assert.Equal("patient@example.test", winner.Data.PatientEmail);
        Assert.Equal("1111111111", winner.Data.PatientPhone);
        Assert.Equal("Synthetic PractitionerRole", winner.Data.PractitionerName);
        Assert.Equal("Synthetic Booker", winner.Data.CreatedByUserName);
        Assert.Equal("Synthetic organization", winner.Data.OrganizationName);
        var loser = Assert.Single(results.Where(result => !result.IsSuccess));
        Assert.Equal(409, loser.Error!.HttpStatus);
        Assert.Null(loser.Data);
        fixture.Context.ChangeTracker.Clear();
        var saved = Assert.Single(await fixture.Context.Appointments
            .Where(appointment => appointment.SessionSlotId == fixture.SlotId).ToListAsync());
        Assert.Equal(winner.Data!.BookingReference, saved.Id.ToString());
        Assert.Equal(AppointmentStatus.Pending, saved.Status);
        Assert.False((await fixture.Context.SessionSlots.SingleAsync(slot => slot.Id == fixture.SlotId)).IsAvailable);
        Assert.DoesNotContain(firstContext.ChangeTracker.Entries<Appointment>(), entry => entry.State == EntityState.Added);
        Assert.DoesNotContain(secondContext.ChangeTracker.Entries<Appointment>(), entry => entry.State == EntityState.Added);
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(HasPostgresConfiguration))]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.CancelRequested)]
    [InlineData(AppointmentStatus.Completed)]
    public async Task NonCancelledBookingPreventsAnotherAndFailedSaveRollsBackSlotMutation(AppointmentStatus status)
    {
        var options = await PrepareDatabase();
        using var fixture = new AppointmentBookingEligibilityTests.Fixture(options);
        var original = await AddHistory(fixture, status);
        // Keep the slot flag deliberately stale/available to reach the DB constraint.
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.Error!.HttpStatus);
        Assert.Null(result.Data);
        Assert.DoesNotContain(fixture.Context.ChangeTracker.Entries<Appointment>(), entry => entry.State == EntityState.Added);
        fixture.Context.ChangeTracker.Clear();
        var saved = Assert.Single(await fixture.Context.Appointments
            .Where(appointment => appointment.SessionSlotId == fixture.SlotId).ToListAsync());
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(status, saved.Status);
        // The attempted true -> false change must be undone along with the failed insert.
        Assert.True((await fixture.Context.SessionSlots.SingleAsync(slot => slot.Id == fixture.SlotId)).IsAvailable);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasPostgresConfiguration))]
    public async Task CancelledHistoryAllowsRebookingAndIsRetained()
    {
        var options = await PrepareDatabase();
        using var fixture = new AppointmentBookingEligibilityTests.Fixture(options);
        var cancelled = await AddHistory(fixture, AppointmentStatus.Cancelled);
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);

        Assert.True(result.IsSuccess);
        fixture.Context.ChangeTracker.Clear();
        var history = await fixture.Context.Appointments
            .Where(appointment => appointment.SessionSlotId == fixture.SlotId).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, appointment => appointment.Id == cancelled.Id && appointment.Status == AppointmentStatus.Cancelled);
        var active = Assert.Single(history.Where(appointment => appointment.Status != AppointmentStatus.Cancelled));
        Assert.Equal(result.Data!.BookingReference, active.Id.ToString());
        Assert.False((await fixture.Context.SessionSlots.SingleAsync(slot => slot.Id == fixture.SlotId)).IsAvailable);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasPostgresConfiguration))]
    public async Task StringRequesterCanRequestCancellationCreatorCanConfirmAndSlotCanBeRebooked()
    {
        var options = await PrepareDatabase();
        using var fixture = new AppointmentBookingEligibilityTests.Fixture(options);
        var owner = TestProfiles.UserId(await fixture.Context.Patients.SingleAsync(patient => patient.Id == fixture.PatientId));
        var booked = await fixture.Service.Create(fixture.Request(), owner);
        Assert.True(booked.IsSuccess);
        var appointmentId = Guid.Parse(booked.Data!.BookingReference);
        var slot = await fixture.Context.SessionSlots.Include(value => value.Session)
            .SingleAsync(value => value.Id == fixture.SlotId);
        var requesterId = "front-office-" + Guid.NewGuid().ToString("N");
        fixture.Context.Users.Add(new ApplicationUser { Id = requesterId, FirstName = "Synthetic", LastName = "OrganizationMembership" });
        fixture.Context.OrganizationMemberships.Add(new OrganizationMembership
        {
            Id = Guid.NewGuid(), UserId = requesterId,

            OrganizationId = slot.Session.OrganizationId,
        });
        await TestProfiles.SaveAsync(fixture.Context);
        fixture.Context.ChangeTracker.Clear();
        var dto = new CancellationRequestDto { AppointmentId = appointmentId.ToString(), Reason = "Synthetic reason" };

        var requested = await fixture.Service.Cancel(dto, requesterId, [Permissions.AppointmentsManage]);
        Assert.True(requested.IsSuccess);
        Assert.Equal(AppointmentStatus.CancelRequested, requested.Data!.Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.False((await fixture.Context.SessionSlots.SingleAsync(value => value.Id == fixture.SlotId)).IsAvailable);

        var confirmed = await fixture.Service.Cancel(dto, owner, ["patient"]);
        Assert.True(confirmed.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, confirmed.Data!.Status);
        fixture.Context.ChangeTracker.Clear();
        var request = await fixture.Context.CancellationRequests.SingleAsync(value => value.AppointmentId == appointmentId);
        Assert.Equal(requesterId, request.RequestedById);
        Assert.Equal(AppointmentStatus.Cancelled, request.Status);
        Assert.True((await fixture.Context.SessionSlots.SingleAsync(value => value.Id == fixture.SlotId)).IsAvailable);

        var rebooked = await fixture.Service.Create(fixture.Request(), owner);
        Assert.True(rebooked.IsSuccess);
        fixture.Context.ChangeTracker.Clear();
        var history = await fixture.Context.Appointments.Where(value => value.SessionSlotId == fixture.SlotId).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Single(history.Where(value => value.Status == AppointmentStatus.Cancelled));
        Assert.Single(history.Where(value => value.Status == AppointmentStatus.Pending));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasPostgresConfiguration))]
    public async Task InitialMigrationCreatesCorrectedRequesterColumnAndOneMigrationHistory()
    {
        var options = await PrepareDatabase();
        await using var context = new ApplicationDbContext(options);
        Assert.Single(context.Database.GetMigrations());
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT data_type || ':' || is_nullable FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'CancellationRequests' AND column_name = 'RequestedById'
            """;
        Assert.Equal("text:NO", await command.ExecuteScalarAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasPostgresConfiguration))]
    public async Task FailedProfileSaveRollsBackTheIdentityAccountAndRole()
    {
        var options = await PrepareDatabase();
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!).ConnectionString;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(builder => builder.UseNpgsql(connection).AddInterceptors(new RejectPatientSave()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("patient"))
            Assert.True((await roles.CreateAsync(new IdentityRole("patient"))).Succeeded);
        var identity = new IdentityGateway(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            roles, NullLogger<IdentityGateway>.Instance, null!);
        var users = new UserService(new UserRepository(context), null!);
        var auth = new AuthService(identity, users);
        var patients = new PatientService(auth, new PatientRepository(context), new RegistrationTransactions(context), new ClinicalRecordRepository(context));
        var email = $"rollback-{Guid.NewGuid():N}@example.test";
        var request = new CreatePatientDto
        {
            PersonalInformation = new RegisterUserDto
            {
                FirstName = "Synthetic", LastName = "Rollback", Email = email,
                PhoneNumber = "0000000000", Password = "SyntheticTest1!"
            },
            Gender = "Test", BirthDate = new DateOnly(2000, 1, 1)
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => patients.CreatePatient(request));
        Assert.Equal("Synthetic profile save failure", error.Message);
        await using var verification = new ApplicationDbContext(options);
        Assert.False(await verification.Users.AnyAsync(user => user.Email == email));
        Assert.False(await verification.People.AnyAsync(person => person.Email == email));
        Assert.False(await verification.Patients.Join(verification.Users,
            patient => patient.PersonId, user => user.PersonId, (patient, user) => user)
            .AnyAsync(user => user.Email == email));
    }

    private sealed class RejectPatientSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Patient>().Any(entry => entry.State == EntityState.Added))
                throw new InvalidOperationException("Synthetic profile save failure");
            return ValueTask.FromResult(result);
        }
    }

    private static async Task<DbContextOptions<ApplicationDbContext>> PrepareDatabase()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!);
        if (!string.Equals(connection.Database, "nexmed360_refactored_tests", StringComparison.Ordinal))
            throw new InvalidOperationException("Booking tests require the separate database named nexmed360_refactored_tests.");

        // Match the application's existing timestamp behavior; never load appsettings.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection.ConnectionString).Options;
        await using var context = new ApplicationDbContext(options);
        // Opening first requires the manually created target to exist; no DB creation/deletion.
        await context.Database.OpenConnectionAsync();
        await context.Database.MigrateAsync();
        return options;
    }

    private static AppointmentService Service(ApplicationDbContext context)
    {
        return new AppointmentService(new AppointmentRepository(context));
    }

    private static async Task<Appointment> AddHistory(
        AppointmentBookingEligibilityTests.Fixture fixture, AppointmentStatus status)
    {
        var slot = await fixture.Context.SessionSlots.Include(slot => slot.Session)
            .SingleAsync(slot => slot.Id == fixture.SlotId);
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(), PatientId = fixture.PatientId,
            PractitionerRoleId = slot.Session.PractitionerRoleId, OrganizationId = slot.Session.OrganizationId,
            CreatedByUserId = fixture.RequesterId, SessionSlotId = slot.Id,
            AppointmentDateTime = slot.Date.ToDateTime(slot.StartTime), Number = slot.SlotNumber,
            Status = status
        };
        fixture.Context.Appointments.Add(appointment);
        await TestProfiles.SaveAsync(fixture.Context);
        return appointment;
    }

    private sealed class BothRequestsReady : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;
        public int RequestCount => _requestCount;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Appointment>()
                .Any(entry => entry.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _requestCount) == 2)
                    _ready.TrySetResult(true);
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
