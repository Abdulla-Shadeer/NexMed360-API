using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Services;
using BookingFixture = NexMed360.API.Old.Tests.AppointmentBookingEligibilityTests.Fixture;

namespace NexMed360.API.Old.Tests;

public class AppointmentStatusAccessTests
{
    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.InProgress)]
    public async Task AssignedDoctorCanPerformExistingStatusActions(AppointmentStatus status)
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = (await fixture.Context.Doctors.SingleAsync()).UserId;

        var result = await fixture.Service.ChangeStatus(id, status, caller, [UserRoles.DOCTOR]);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Data!.BookingReference);
        Assert.Equal(status, result.Data.Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(status, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.False((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Arrived)]
    public async Task SameHospitalReceptionistCanPerformExistingFrontOfficeActions(AppointmentStatus status)
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = await AppointmentReadAccessTests.AddReceptionist(fixture, true);

        var result = await fixture.Service.ChangeStatus(id, status, caller, [UserRoles.RECEPTIONIST]);

        Assert.True(result.IsSuccess);
        Assert.Equal(status, result.Data!.Status);
    }

    [Fact]
    public async Task OtherHospitalReceptionistCannotMutateAppointment()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = await AppointmentReadAccessTests.AddReceptionist(fixture, false);

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, caller, [UserRoles.RECEPTIONIST]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.False((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Fact]
    public async Task AnotherDoctorAtSameHospitalCannotMutateAppointment()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var hospital = await fixture.Context.Hospitals.SingleAsync();
        var user = new User { Id = "other-doctor", FirstName = "Other", LastName = "Doctor" };
        fixture.Context.Users.Add(user);
        if (fixture.Context.Entry(hospital).State == EntityState.Detached) fixture.Context.Hospitals.Add(hospital);
        fixture.Context.Doctors.Add(new Doctor
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            HospitalId = hospital.Id,  Speciality = "Test speciality"
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Completed, user.Id, [UserRoles.DOCTOR]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
    }

    [Fact]
    public async Task PatientScopeDoesNotGrantStaffPermissionsEvenWithAnUnassignedDoctorRole()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = (await fixture.Context.Patients.SingleAsync()).UserId;

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed,
            caller, [UserRoles.PATIENT, UserRoles.DOCTOR]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
    }

    [Fact]
    public async Task ReceptionistScopeDoesNotAllowStartingAnEncounterEvenWithAnUnassignedDoctorRole()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = await AppointmentReadAccessTests.AddReceptionist(fixture, true);

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.InProgress,
            caller, [UserRoles.RECEPTIONIST, UserRoles.DOCTOR]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(UserRoles.PATIENT, AppointmentStatus.Confirmed)]
    [InlineData(UserRoles.SUPERUSER, AppointmentStatus.Completed)]
    [InlineData(UserRoles.RECEPTIONIST, AppointmentStatus.InProgress)]
    public async Task UnsupportedRoleIsRejectedBeforeStorageAccess(string role, AppointmentStatus status)
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());

        var result = await new AppointmentService(new AppointmentRepository(context), TestViews.For(context)).ChangeStatus(
            Guid.NewGuid().ToString(), status, "signed-in-user", [role]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(403, result.Error!.HttpStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingCallerIdentityIsRejectedBeforeStorageAccess(string? caller)
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());

        var result = await new AppointmentService(new AppointmentRepository(context), TestViews.For(context)).ChangeStatus(
            Guid.NewGuid().ToString(), AppointmentStatus.Confirmed, caller, [UserRoles.DOCTOR]);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task MissingAndMalformedIdsKeepExistingResponses()
    {
        using var fixture = new BookingFixture();
        await AppointmentReadAccessTests.Book(fixture);
        var caller = (await fixture.Context.Doctors.SingleAsync()).UserId;

        var missing = await fixture.Service.ChangeStatus(Guid.NewGuid().ToString(),
            AppointmentStatus.Confirmed, caller, [UserRoles.DOCTOR]);
        var malformed = await fixture.Service.ChangeStatus("invalid",
            AppointmentStatus.Confirmed, caller, [UserRoles.DOCTOR]);

        Assert.Equal(404, missing.Error!.HttpStatus);
        Assert.Equal(ErrorCode.APPT_NotFound, missing.Error.Code);
        Assert.Equal(400, malformed.Error!.HttpStatus);
        Assert.Equal(ErrorCode.SYS_InvalidGuid, malformed.Error.Code);
    }

    [Fact]
    public async Task AuthorizedCancelledAppointmentKeepsGoneResponseAndUnrelatedCallerGetsNotFound()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        (await fixture.Context.Appointments.SingleAsync()).Status = AppointmentStatus.Cancelled;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var caller = (await fixture.Context.Doctors.SingleAsync()).UserId;

        var own = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, caller, [UserRoles.DOCTOR]);
        var other = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, "unrelated-doctor", [UserRoles.DOCTOR]);

        Assert.Equal(410, own.Error!.HttpStatus);
        Assert.Equal(404, other.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Cancelled, (await fixture.Context.Appointments.SingleAsync()).Status);
    }

    [Fact]
    public async Task CancellationCleanupAndStatusUpdateAreSubmittedInOneSave()
    {
        var probe = new StatusSaveProbe();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(probe).Options;
        using var fixture = new BookingFixture(options);
        var id = await AppointmentReadAccessTests.Book(fixture);
        await AddCancellationRequest(fixture);
        var caller = (await fixture.Context.Doctors.SingleAsync()).UserId;
        probe.SaveCount = 0;

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, caller, [UserRoles.DOCTOR]);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, probe.SaveCount);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Confirmed, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.Empty(await fixture.Context.CancellationRequests.ToListAsync());
    }

    [Fact]
    public async Task FailedStatusSaveDoesNotLeaveCancellationCleanupPersistedSeparately()
    {
        var probe = new StatusSaveProbe();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(probe).Options;
        using var fixture = new BookingFixture(options);
        var id = await AppointmentReadAccessTests.Book(fixture);
        var cancellationId = await AddCancellationRequest(fixture);
        var caller = (await fixture.Context.Doctors.SingleAsync()).UserId;
        probe.SaveCount = 0;
        probe.FailWhenAppointmentModified = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ChangeStatus(
            id, AppointmentStatus.Confirmed, caller, [UserRoles.DOCTOR]));

        Assert.Equal(1, probe.SaveCount);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.CancelRequested, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.Equal(cancellationId, (await fixture.Context.CancellationRequests.SingleAsync()).Id);
    }

    [Fact]
    public async Task UnrelatedCallerCannotRemoveAnExistingCancellationRequest()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var cancellationId = await AddCancellationRequest(fixture);

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed,
            "unrelated-doctor", [UserRoles.DOCTOR]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.CancelRequested, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.Equal(cancellationId, (await fixture.Context.CancellationRequests.SingleAsync()).Id);
    }

    [Fact]
    public async Task MissingCancellationRowDoesNotBreakAuthorizedStatusReset()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        (await fixture.Context.Appointments.SingleAsync()).Status = AppointmentStatus.CancelRequested;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var caller = (await fixture.Context.Doctors.SingleAsync()).UserId;

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, caller, [UserRoles.DOCTOR]);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Confirmed, result.Data!.Status);
        Assert.Empty(await fixture.Context.CancellationRequests.ToListAsync());
    }

    private static async Task<Guid> AddCancellationRequest(BookingFixture fixture)
    {
        var appointment = await fixture.Context.Appointments.SingleAsync();
        appointment.Status = AppointmentStatus.CancelRequested;
        var cancellation = new CancellationRequest
        {
            Id = Guid.NewGuid(), AppointmentId = appointment.Id, RequestedById = Guid.NewGuid().ToString(),
            Reason = "Synthetic reason"
        };
        fixture.Context.CancellationRequests.Add(cancellation);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        return cancellation.Id;
    }

    private sealed class StatusSaveProbe : SaveChangesInterceptor
    {
        public int SaveCount { get; set; }
        public bool FailWhenAppointmentModified { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (FailWhenAppointmentModified && eventData.Context!.ChangeTracker.Entries<Appointment>()
                .Any(entry => entry.State == EntityState.Modified))
                throw new InvalidOperationException("Synthetic status-save failure");
            return ValueTask.FromResult(result);
        }
    }
}
