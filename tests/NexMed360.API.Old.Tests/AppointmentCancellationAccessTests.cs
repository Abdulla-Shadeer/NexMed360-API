using NexMed360.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Services;
using BookingFixture = NexMed360.API.Old.Tests.AppointmentBookingEligibilityTests.Fixture;

namespace NexMed360.API.Old.Tests;

public class AppointmentCancellationAccessTests
{
    [Theory]
    [InlineData("patient")]
    [InlineData(Permissions.EncountersStart)]
    [InlineData(Permissions.AppointmentsManage)]
    public async Task RelatedCallerKeepsExistingCreatorOrRequestBehaviorAndStoresExactStringId(string role)
    {
        using var fixture = new BookingFixture();
        var (id, owner) = await BookAsPatient(fixture);
        var caller = role switch
        {
            "patient" => owner,
            Permissions.EncountersStart => TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync()),
            _ => await AppointmentReadAccessTests.AddOrganizationMember(fixture, true)
        };
        Assert.False(Guid.TryParse(caller, out _));

        var result = await fixture.Service.Cancel(Request(id), caller, [role]);

        var expected = caller == owner ? AppointmentStatus.Cancelled : AppointmentStatus.CancelRequested;
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Data!.Status);
        fixture.Context.ChangeTracker.Clear();
        var request = await fixture.Context.CancellationRequests.SingleAsync();
        Assert.Equal(caller, request.RequestedById);
        Assert.Equal(expected, request.Status);
        Assert.Equal(expected, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.Equal(caller == owner, (await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Fact]
    public async Task CreatorConfirmsExistingRequestWithoutReplacingItsOriginalRequesterOrReason()
    {
        using var fixture = new BookingFixture();
        var (id, owner) = await BookAsPatient(fixture);
        var practitionerRole = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());
        Assert.True((await fixture.Service.Cancel(Request(id), practitionerRole, [Permissions.EncountersStart])).IsSuccess);

        var result = await fixture.Service.Cancel(
            new CancellationRequestDto { AppointmentId = id, Reason = "Creator confirmation" }, owner, ["patient"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, result.Data!.Status);
        fixture.Context.ChangeTracker.Clear();
        var request = await fixture.Context.CancellationRequests.SingleAsync();
        Assert.Equal(practitionerRole, request.RequestedById);
        Assert.Equal("Synthetic reason", request.Reason);
        Assert.Equal(AppointmentStatus.Cancelled, request.Status);
        Assert.True((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Fact]
    public async Task RepeatedNonCreatorRequestKeepsExistingConflictAndDoesNotAddOrReleaseAnything()
    {
        using var fixture = new BookingFixture();
        var (id, _) = await BookAsPatient(fixture);
        var practitionerRole = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());
        await fixture.Service.Cancel(Request(id), practitionerRole, [Permissions.EncountersStart]);

        var result = await fixture.Service.Cancel(Request(id), practitionerRole, [Permissions.EncountersStart]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.APPT_CancellationAlreadyRequested, result.Error!.Code);
        Assert.Equal(409, result.Error.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Single(await fixture.Context.CancellationRequests.ToListAsync());
        Assert.Equal(AppointmentStatus.CancelRequested, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.False((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Fact]
    public async Task OtherOrganizationOrganizationMemberCannotCancelOrRequestCancellation()
    {
        using var fixture = new BookingFixture();
        var (id, _) = await BookAsPatient(fixture);
        var caller = await AppointmentReadAccessTests.AddOrganizationMember(fixture, false);

        var result = await fixture.Service.Cancel(Request(id), caller, [Permissions.AppointmentsManage]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
        await AssertStillBookedWithoutRequest(fixture);
    }

    [Fact]
    public async Task UnrelatedCreatorDoesNotGainAccessByHavingCreatedTheBooking()
    {
        using var fixture = new BookingFixture();
        var id = await AppointmentReadAccessTests.Book(fixture);

        var result = await fixture.Service.Cancel(Request(id), fixture.RequesterId, ["patient"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        await AssertStillBookedWithoutRequest(fixture);
    }

    [Theory]
    [InlineData("patient")]
    [InlineData(UserRoles.PLATFORM_ADMIN)]
    public async Task StoredPractitionerRoleProfileWithoutItsTokenRoleCannotCancel(string tokenRole)
    {
        using var fixture = new BookingFixture();
        var (id, _) = await BookAsPatient(fixture);
        var practitionerRole = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());

        var result = await fixture.Service.Cancel(Request(id), practitionerRole, [tokenRole]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        await AssertStillBookedWithoutRequest(fixture);
    }

    [Fact]
    public async Task ExistingRequestCannotBeChangedByAnUnrelatedCaller()
    {
        using var fixture = new BookingFixture();
        var (id, _) = await BookAsPatient(fixture);
        var practitionerRole = TestProfiles.UserId(await fixture.Context.PractitionerRoles.SingleAsync());
        await fixture.Service.Cancel(Request(id), practitionerRole, [Permissions.EncountersStart]);
        fixture.Context.ChangeTracker.Clear();
        var originalId = (await fixture.Context.CancellationRequests.SingleAsync()).Id;

        var result = await fixture.Service.Cancel(Request(id), "unrelated-practitionerRole", [Permissions.EncountersStart]);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        var request = await fixture.Context.CancellationRequests.SingleAsync();
        Assert.Equal(originalId, request.Id);
        Assert.Equal(practitionerRole, request.RequestedById);
        Assert.Equal(AppointmentStatus.CancelRequested, request.Status);
        Assert.False((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingIdentityFailsBeforeStorageAccess(string? caller)
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());

        var result = await new AppointmentService(new AppointmentRepository(context)).Cancel(
            Request(Guid.NewGuid().ToString()), caller, ["patient"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task MissingMalformedAndCancelledTargetsKeepExistingResponses()
    {
        using var fixture = new BookingFixture();
        var (id, owner) = await BookAsPatient(fixture);
        Assert.True((await fixture.Service.Cancel(Request(id), owner, ["patient"])).IsSuccess);

        var missing = await fixture.Service.Cancel(Request(Guid.NewGuid().ToString()), owner, ["patient"]);
        var malformed = await fixture.Service.Cancel(Request("invalid"), owner, ["patient"]);
        var cancelled = await fixture.Service.Cancel(Request(id), owner, ["patient"]);
        var unrelated = await fixture.Service.Cancel(Request(id), "unrelated-user", ["patient"]);

        Assert.Equal(404, missing.Error!.HttpStatus);
        Assert.Equal(400, malformed.Error!.HttpStatus);
        Assert.Equal(410, cancelled.Error!.HttpStatus);
        Assert.Equal(404, unrelated.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Single(await fixture.Context.CancellationRequests.ToListAsync());
    }

    [Fact]
    public async Task CreatorCanCompleteCancellationWhenThePendingRequestRowIsMissing()
    {
        using var fixture = new BookingFixture();
        var (id, owner) = await BookAsPatient(fixture);
        (await fixture.Context.Appointments.SingleAsync()).Status = AppointmentStatus.CancelRequested;
        await TestProfiles.SaveAsync(fixture.Context);
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.Cancel(Request(id), owner, ["patient"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, result.Data!.Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(owner, (await fixture.Context.CancellationRequests.SingleAsync()).RequestedById);
        Assert.True((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatorCancellationUsesOneSaveAndFailureLeavesAllStoredStateUntouched(bool fail)
    {
        var probe = new CancellationSaveProbe();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(probe).Options;
        using var fixture = new BookingFixture(options);
        var (id, owner) = await BookAsPatient(fixture);
        probe.SaveCount = 0;
        probe.Fail = fail;

        if (fail)
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.Cancel(Request(id), owner, ["patient"]));
        else
            Assert.True((await fixture.Service.Cancel(Request(id), owner, ["patient"])).IsSuccess);

        Assert.Equal(1, probe.SaveCount);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(fail ? AppointmentStatus.Pending : AppointmentStatus.Cancelled,
            (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.Equal(!fail, (await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
        Assert.Equal(fail ? 0 : 1, await fixture.Context.CancellationRequests.CountAsync());
    }

    private static CancellationRequestDto Request(string id) => new() { AppointmentId = id, Reason = "Synthetic reason" };

    private static async Task<(string Id, string Owner)> BookAsPatient(BookingFixture fixture)
    {
        var owner = TestProfiles.UserId(await fixture.Context.Patients.SingleAsync());
        var result = await fixture.Service.Create(fixture.Request(), owner);
        Assert.True(result.IsSuccess);
        fixture.Context.ChangeTracker.Clear();
        return (result.Data!.BookingReference, owner);
    }

    private static async Task AssertStillBookedWithoutRequest(BookingFixture fixture)
    {
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Pending, (await fixture.Context.Appointments.SingleAsync()).Status);
        Assert.False((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
        Assert.Empty(await fixture.Context.CancellationRequests.ToListAsync());
    }

    private sealed class CancellationSaveProbe : SaveChangesInterceptor
    {
        public int SaveCount { get; set; }
        public bool Fail { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (Fail)
                throw new InvalidOperationException("Synthetic cancellation-save failure");
            return ValueTask.FromResult(result);
        }
    }
}
