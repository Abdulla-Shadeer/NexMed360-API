using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NexMed360Api.Common;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Services;
using Npgsql;

namespace NexMed360.API.Old.Tests;

[Trait("Category", "PostgreSQL")]
public class AppointmentBookingPostgresTests
{
    public const string ConnectionVariable = "NEXMED360_BOOKING_TEST_POSTGRES";
    private const string SkipReason = "Set NEXMED360_BOOKING_TEST_POSTGRES for the separate nexmed360_booking_tests database.";
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

    private static async Task<DbContextOptions<ApplicationDbContext>> PrepareDatabase()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!);
        if (!string.Equals(connection.Database, "nexmed360_booking_tests", StringComparison.Ordinal))
            throw new InvalidOperationException("Booking tests require the separate database named nexmed360_booking_tests.");

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
        var mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        return new AppointmentService(context, mapper);
    }

    private static async Task<Appointment> AddHistory(
        AppointmentBookingEligibilityTests.Fixture fixture, AppointmentStatus status)
    {
        var slot = await fixture.Context.SessionSlots.Include(slot => slot.Session)
            .SingleAsync(slot => slot.Id == fixture.SlotId);
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(), PatientId = fixture.PatientId,
            DoctorId = slot.Session.DoctorId, HospitalId = slot.Session.HospitalId,
            CreatedByUserId = fixture.RequesterId, SessionSlotId = slot.Id,
            AppointmentDateTime = slot.Date.ToDateTime(slot.StartTime), Number = slot.SlotNumber,
            Status = status
        };
        fixture.Context.Appointments.Add(appointment);
        await fixture.Context.SaveChangesAsync();
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
