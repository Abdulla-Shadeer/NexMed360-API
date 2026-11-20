using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Organizations;
using NexMed360.Infrastructure.Repositories;
using NexMed360Api.Common.Auth;
using NexMed360Api.Entities;
using NexMed360Api.Services;
using NexMed360Api.Interfaces.Services;
using NexMed360.API.Entities.Enums;

namespace NexMed360.API.Old.Tests;

public class ReviewBatchTests
{
    [Fact]
    public async Task LocationManagementKeepsOrganizationScopeAndRejectsParentCycles()
    {
        using var f = new RecordSharingTests.Fixture();
        var foreign = new Location { OrganizationId = f.SourceOrganization.Id, Name = "Other site" };
        f.Context.Locations.Add(foreign); await f.Context.SaveChangesAsync();
        f.Work(); f.Access.Current!.Permissions.Add(Permissions.OrganizationsManage);
        var service = new LocationService(new LocationRepository(f.Context), f.Access);
        var created = await service.Save(null, new("Clinic", "Site", null, "Test address", true));
        Assert.True(created.IsSuccess);
        var child = await service.Save(null, new("Ward", "Ward", created.Data!.Id, null, true));
        Assert.True(child.IsSuccess);
        Assert.Equal(400, (await service.Save(created.Data.Id, new("Clinic", "Site", child.Data!.Id, null, true))).Error!.HttpStatus);
        Assert.Equal(400, (await service.Save(null, new("Room", "Room", foreign.Id, null, true))).Error!.HttpStatus);
        Assert.Equal(404, (await service.Save(foreign.Id, new("Changed", "Site", null, null, true))).Error!.HttpStatus);
        Assert.Equal(400, (await service.Save(created.Data.Id, new("Clinic", "Site", null, null, false))).Error!.HttpStatus);
        var updated = await service.Save(child.Data.Id, new("Updated ward", "Ward", created.Data.Id, null, false));
        Assert.True(updated.IsSuccess);
        Assert.Equal(2, (await service.List()).Data!.Count);
        f.Access.Current.LocationIds = [created.Data.Id];
        Assert.Equal(403, (await service.List()).Error!.HttpStatus);
    }

    [Fact]
    public async Task AppointmentEligibilityIsConfiguredOnAssignmentWithoutClinicalPermissions()
    {
        using var f = new RecordSharingTests.Fixture(); f.Work();
        f.Access.Current!.Permissions = [Permissions.MembersManage];
        var service = new PractitionerBookingService(new PractitionerRoleRepository(f.Context), f.Access);
        var roles = (await service.List(f.StaffUserId)).Data!;
        Assert.False(Assert.Single(roles).AcceptsAppointments);
        Assert.True((await service.Update(f.ReceivingRole.Id, new(true))).IsSuccess);
        Assert.True((await f.Context.PractitionerRoles.SingleAsync()).CanAcceptAppointments(DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.Equal(404, (await service.Update(f.SourceRole.Id, new(true))).Error!.HttpStatus);
        f.Access.Current.Permissions = [];
        Assert.Equal(403, (await service.Update(f.ReceivingRole.Id, new(false))).Error!.HttpStatus);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("inactive")]
    [InlineData("future")]
    [InlineData("expired")]
    public async Task BookingApiRejectsIneligibleAssignmentEvenWhenSlotIdIsKnown(string reason)
    {
        using var f = new AppointmentBookingEligibilityTests.Fixture();
        var role = await f.Context.PractitionerRoles.SingleAsync();
        if (reason == "disabled") role.AcceptsAppointments = false;
        if (reason == "inactive") role.IsActive = false;
        if (reason == "future") role.StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        if (reason == "expired") role.EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await f.Context.SaveChangesAsync();
        var result = await f.Service.Create(f.Request(), f.RequesterId);
        Assert.False(result.IsSuccess);
        Assert.Empty(await f.Context.Appointments.ToListAsync());
    }

    [Fact]
    public async Task OwnReadPermissionsCannotRevealAnotherPractitionersAppointmentsOrSessions()
    {
        using var f = new RecordSharingTests.Fixture();
        var other = new PractitionerRole { OrganizationId = f.ReceivingOrganization.Id, PractitionerId = f.SourceRole.PractitionerId };
        var ownSession = Session(f.ReceivingRole);
        var otherSession = Session(other);
        f.Context.AddRange(other, ownSession, otherSession);
        await f.Context.SaveChangesAsync();
        var slot = ownSession.GenerateSlots(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))[0];
        var otherSlot = otherSession.GenerateSlots(slot.Date)[0];
        f.Context.AddRange(slot, otherSlot);
        var ownAppointment = new Appointment { PatientId = f.Patient.Id, OrganizationId = f.ReceivingOrganization.Id, PractitionerRoleId = f.ReceivingRole.Id, SessionSlotId = slot.Id, CreatedByUserId = f.StaffUserId };
        var otherAppointment = new Appointment { PatientId = f.Patient.Id, OrganizationId = f.ReceivingOrganization.Id, PractitionerRoleId = other.Id, SessionSlotId = otherSlot.Id, CreatedByUserId = f.StaffUserId };
        f.Context.AddRange(ownAppointment, otherAppointment); await f.Context.SaveChangesAsync();
        f.Work(); f.Access.Current!.Permissions = [Permissions.AppointmentsReadOwn, Permissions.SchedulesReadOwn];
        var appointments = new AppointmentRepository(f.Context);
        Assert.Equal(ownAppointment.Id, Assert.Single(await appointments.GetOwn(new(null, null, null, null, null, null), f.StaffUserId)).Id);
        Assert.Null(await appointments.FindReadable(otherAppointment.Id, f.StaffUserId, [Permissions.AppointmentsReadOwn]));
        var schedules = new PractitionerScheduleService(new PractitionerScheduleRepository(f.Context), f.Access);
        Assert.Equal(ownSession.Id, Assert.Single((await schedules.Get(new())).Data!).Id);
        f.Access.Current.Permissions.Add(Permissions.OrganizationSchedulesManage);
        Assert.Equal(2, (await schedules.Get(new())).Data!.Count);
        Assert.Equal(ownSession.Id, Assert.Single((await schedules.Get(new() { OwnOnly = true })).Data!).Id);
    }

    [Fact]
    public async Task BookingSearchChecksFreeSlotsForTheRequestedDateAndKeepsAdminSearchSeparate()
    {
        using var f = new RecordSharingTests.Fixture();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var role = await f.Context.PractitionerRoles.SingleAsync(row => row.Id == f.ReceivingRole.Id);
        role.AcceptsAppointments = true;
        var session = Session(role); f.Context.Add(session); await f.Context.SaveChangesAsync();
        f.Work();
        var search = new SearchRepository(f.Context, new PractitionerRoleRepository(f.Context), null!);
        Assert.Equal(role.Id.ToString(), Assert.Single(await search.PractitionerRoles(null, null, null, true, date)).Id);
        var slots = session.GenerateSlots(date); foreach (var slot in slots) slot.IsAvailable = false;
        f.Context.AddRange(slots); await f.Context.SaveChangesAsync();
        Assert.Empty(await search.PractitionerRoles(null, null, null, true, date));
        Assert.Single(await search.PractitionerRoles(null, null, null));
    }

    [Fact]
    public async Task SessionHistorySupportsOrganizationAndOwnViewsWithInclusiveDates()
    {
        using var f = new RecordSharingTests.Fixture();
        var other = new PractitionerRole { OrganizationId = f.ReceivingOrganization.Id, PractitionerId = f.SourceRole.PractitionerId };
        var own = Session(f.ReceivingRole); var colleague = Session(other);
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        f.Context.AddRange(other, own, colleague,
            new SessionHistory { OrganizationId = f.ReceivingOrganization.Id, PractitionerScheduleId = own.Id, Date = day },
            new SessionHistory { OrganizationId = f.ReceivingOrganization.Id, PractitionerScheduleId = colleague.Id, Date = day });
        await f.Context.SaveChangesAsync(); f.Work();
        f.Access.Current!.Permissions = [Permissions.OrganizationSchedulesManage, Permissions.SchedulesReadOwn];
        var service = new PractitionerScheduleService(new PractitionerScheduleRepository(f.Context), f.Access);
        Assert.Equal(2, (await service.GetHistory(new() { FromDate = day, ToDate = day })).Data!.Count);
        Assert.Single((await service.GetHistory(new() { OwnOnly = true })).Data!);
        f.Access.Current.Permissions = [Permissions.SchedulesReadOwn];
        Assert.Single((await service.GetHistory(new())).Data!);
    }

    private static PractitionerSchedule Session(PractitionerRole role) => new()
    {
        OrganizationId = role.OrganizationId, PractitionerRoleId = role.Id,
        StartTime = new(9, 0), EndTime = new(10, 0), Occurrences = [SessionOccurrence.AllDays]
    };
}
