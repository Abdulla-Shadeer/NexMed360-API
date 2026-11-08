using NexMed360.Infrastructure.Repositories;
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
    public async Task AssignedPractitionerRoleCanPerformExistingStatusActions(AppointmentStatus status)
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());

        var result = await fixture.Service.ChangeStatus(id, status, caller, [Permissions.EncountersStart]);

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
    public async Task SameOrganizationOrganizationMemberCanPerformExistingFrontOfficeActions(AppointmentStatus status)
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = await AppointmentReadAccessTests.AddOrganizationMember(fixture, true);

        var result = await fixture.Service.ChangeStatus(id, status, caller, [Permissions.AppointmentsManage]);

        Assert.True(result.IsSuccess);
        Assert.Equal(status, result.Data!.Status);
    }

    [Fact]
    public async Task OtherOrganizationOrganizationMemberCannotMutateAppointment()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = await AppointmentReadAccessTests.AddOrganizationMember(fixture, false);

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, caller, [Permissions.AppointmentsManage]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.False((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Fact]
    public async Task AnotherPractitionerRoleAtSameOrganizationCannotMutateAppointment()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var organization = await fixture.Context.Organizations.SingleAsync();
        var user = new ApplicationUser { Id = "other-practitionerRole", FirstName = "Other", LastName = "PractitionerRole" };
        fixture.Context.Users.Add(user);
        if (fixture.Context.Entry(organization).State == EntityState.Detached) fixture.Context.Organizations.Add(organization);
        fixture.Context.PractitionerRoles.Add(new PractitionerRole
        {
            Id = Guid.NewGuid(), PractitionerId = TestProfiles.PractitionerId(user.Id),
            OrganizationId = organization.Id,  Specialty = "Test specialty"
        });
        await TestProfiles.SaveAsync(fixture.Context);
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Completed, user.Id, [Permissions.EncountersStart]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
    }

    [Fact]
    public async Task PatientScopeDoesNotGrantStaffPermissionsEvenWithAnUnassignedPractitionerRoleRole()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = TestProfiles.UserId(await fixture.Context.Patients.SingleAsync());

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed,
            caller, ["patient", Permissions.EncountersStart]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
    }

    [Fact]
    public async Task OrganizationMemberScopeDoesNotAllowStartingAnEncounterEvenWithAnUnassignedPractitionerRoleRole()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);
        var caller = await AppointmentReadAccessTests.AddOrganizationMember(fixture, true);

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.InProgress,
            caller, [Permissions.AppointmentsManage, Permissions.EncountersStart]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("patient", AppointmentStatus.Confirmed)]
    [InlineData(UserRoles.PLATFORM_ADMIN, AppointmentStatus.Completed)]
    [InlineData(Permissions.AppointmentsManage, AppointmentStatus.InProgress)]
    public async Task UnsupportedRoleIsRejectedBeforeStorageAccess(string role, AppointmentStatus status)
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());

        var result = await new AppointmentService(new AppointmentRepository(context)).ChangeStatus(
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

        var result = await new AppointmentService(new AppointmentRepository(context)).ChangeStatus(
            Guid.NewGuid().ToString(), AppointmentStatus.Confirmed, caller, [Permissions.EncountersStart]);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task MissingAndMalformedIdsKeepExistingResponses()
    {
        using var fixture = new BookingFixture();
        await AppointmentReadAccessTests.Book(fixture);
        var caller = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());

        var missing = await fixture.Service.ChangeStatus(Guid.NewGuid().ToString(),
            AppointmentStatus.Confirmed, caller, [Permissions.EncountersStart]);
        var malformed = await fixture.Service.ChangeStatus("invalid",
            AppointmentStatus.Confirmed, caller, [Permissions.EncountersStart]);

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
        await TestProfiles.SaveAsync(fixture.Context);
        fixture.Context.ChangeTracker.Clear();
        var caller = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());

        var own = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, caller, [Permissions.EncountersStart]);
        var other = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, "unrelated-practitionerRole", [Permissions.EncountersStart]);

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
        var caller = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());
        probe.SaveCount = 0;

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, caller, [Permissions.EncountersStart]);

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
        var caller = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());
        probe.SaveCount = 0;
        probe.FailWhenAppointmentModified = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ChangeStatus(
            id, AppointmentStatus.Confirmed, caller, [Permissions.EncountersStart]));

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
            "unrelated-practitionerRole", [Permissions.EncountersStart]);

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
        await TestProfiles.SaveAsync(fixture.Context);
        fixture.Context.ChangeTracker.Clear();
        var caller = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());

        var result = await fixture.Service.ChangeStatus(id, AppointmentStatus.Confirmed, caller, [Permissions.EncountersStart]);

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
        await TestProfiles.SaveAsync(fixture.Context);
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
