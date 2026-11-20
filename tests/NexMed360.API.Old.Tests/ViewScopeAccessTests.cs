using System.Reflection;
using NexMed360.Application.Access;
using NexMed360.Infrastructure.Repositories;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Controllers.v1;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Services;
using NexMed360.API.Entities.Enums;

namespace NexMed360.API.Old.Tests;

public class ViewScopeAccessTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task PersonalViewsNeedBothAnAssignmentAndOwnReadPermission(bool hasAssignment, bool hasOwnPermission)
    {
        using var f = new RecordSharingTests.Fixture();
        await Seed(f);
        f.Work();
        f.Access.Current!.Permissions = [Permissions.OrganizationAppointmentsManage, Permissions.OrganizationSchedulesManage];
        if (hasOwnPermission) f.Access.Current.Permissions.AddRange([Permissions.AppointmentsReadOwn, Permissions.SchedulesReadOwn]);
        if (!hasAssignment)
        {
            f.Access.Current.PractitionerRoleIds = [];
            f.Access.Current.PractitionerRoleId = null;
        }
        var appointments = Appointments(f);
        var schedules = Schedules(f);
        Assert.Equal(403, (await appointments.GetOwn(new(), f.StaffUserId)).Error!.HttpStatus);
        Assert.Equal(403, (await schedules.Get(new() { OwnOnly = true })).Error!.HttpStatus);
        Assert.Equal(403, (await schedules.GetHistory(new() { OwnOnly = true })).Error!.HttpStatus);
        Assert.Equal(3, (await appointments.GetOrganizationAppointments(new(), f.StaffUserId)).Data!.Count);
        Assert.Equal(3, (await schedules.Get(new())).Data!.Count);
    }

    [Fact]
    public async Task PersonalViewsUseAllPermittedAssignmentsAndExcludeBookingsCreatedForColleagues()
    {
        using var f = new RecordSharingTests.Fixture();
        var (second, colleague) = await Seed(f);
        f.Work();
        f.Access.Current!.PractitionerRoleIds.Add(second.Id);
        f.Access.Current.PractitionerRoleId = null;
        f.Access.Current.Permissions = [Permissions.AppointmentsReadOwn, Permissions.SchedulesReadOwn];
        var appointments = Appointments(f);
        var schedules = Schedules(f);
        Assert.Equal(2, (await appointments.GetOwn(new(), f.StaffUserId)).Data!.Count);
        Assert.Equal(2, (await schedules.Get(new() { OwnOnly = true })).Data!.Count);
        Assert.Equal(2, (await schedules.Get(new())).Data!.Count);
        Assert.Equal(2, (await schedules.GetHistory(new())).Data!.Count);
        Assert.Empty((await appointments.GetOwn(new() { PractitionerRoleId = colleague.Id.ToString() }, f.StaffUserId)).Data!);
        Assert.Empty((await schedules.Get(new() { PractitionerRoleId = colleague.Id.ToString() })).Data!);
        Assert.Equal(403, (await appointments.GetOrganizationAppointments(new(), f.StaffUserId)).Error!.HttpStatus);
    }

    [Fact]
    public async Task OrganizationReadPermissionsKeepOrganizationAndLocationScopeWithoutGrantingManagement()
    {
        using var f = new RecordSharingTests.Fixture();
        await Seed(f);
        var location = new Location { OrganizationId = f.ReceivingOrganization.Id, Name = "Allowed ward" };
        f.Context.Add(location);
        var role = await f.Context.PractitionerRoles.FindAsync(f.ReceivingRole.Id);
        role!.LocationId = location.Id;
        await f.Context.SaveChangesAsync();
        f.Work();
        f.Access.Current!.LocationIds = [location.Id];
        f.Access.Current.PractitionerRoleIds = [];
        f.Access.Current.PractitionerRoleId = null;
        f.Access.Current.Permissions = [Permissions.AppointmentsReadOrganization, Permissions.SchedulesReadOrganization];
        var appointments = Appointments(f);
        var list = (await appointments.GetOrganizationAppointments(new(), f.StaffUserId)).Data!;
        var appointment = Assert.Single(list);
        Assert.True((await appointments.Get(appointment.BookingReference, f.StaffUserId, f.Access.Current.Permissions)).IsSuccess);
        Assert.Single((await Schedules(f).Get(new())).Data!);
        Assert.Single((await Schedules(f).GetHistory(new())).Data!);
        Assert.Equal(403, (await appointments.Cancel(new() { AppointmentId = appointment.BookingReference, Reason = "Test" }, f.StaffUserId, f.Access.Current.Permissions)).Error!.HttpStatus);
        Assert.Equal(403, (await appointments.ChangeStatus(appointment.BookingReference, AppointmentStatus.Confirmed, f.StaffUserId, f.Access.Current.Permissions)).Error!.HttpStatus);
        Assert.Equal(403, (await Schedules(f).GetHistory(new() { OwnOnly = true })).Error!.HttpStatus);
    }

    [Fact]
    public async Task BookingStaffCanReadTheSelectedScheduleWithoutPersonalOrOrganizationSessionViews()
    {
        using var f = new RecordSharingTests.Fixture();
        await Seed(f);
        f.Work();
        f.Access.Current!.Permissions = [Permissions.OrganizationAppointmentsManage];
        f.Access.Current.PractitionerRoleIds = [];
        f.Access.Current.PractitionerRoleId = null;
        var schedules = Schedules(f);
        Assert.Equal(403, (await schedules.Get(new())).Error!.HttpStatus);
        Assert.Single((await schedules.Get(new() { PractitionerRoleId = f.ReceivingRole.Id.ToString() })).Data!);
        Assert.Equal(403, (await schedules.Get(new() { OwnOnly = true, PractitionerRoleId = f.ReceivingRole.Id.ToString() })).Error!.HttpStatus);
        Assert.Equal(403, (await schedules.GetHistory(new())).Error!.HttpStatus);
    }

    [Fact]
    public async Task PatientModeReadsOnlyItsLinkedAppointmentsWithoutStaffPermissions()
    {
        using var f = new RecordSharingTests.Fixture();
        await Seed(f);
        f.PatientMode();
        var list = (await Appointments(f).GetOwn(new(), f.PatientUserId)).Data!;
        Assert.Equal(4, list.Count);
        Assert.All(list, row => Assert.Equal(f.Patient.Id.ToString(), row.PatientId));
        Assert.Equal(403, (await Appointments(f).GetOrganizationAppointments(new(), f.PatientUserId)).Error!.HttpStatus);
    }

    [Fact]
    public void OrganizationReadPermissionsDoNotOpenMutationEndpoints()
    {
        foreach (var method in typeof(AppointmentsController).GetMethods().Concat(typeof(PractitionerScheduleController).GetMethods()))
        {
            var rule = method.GetCustomAttribute<WorkspaceAttribute>();
            if (rule?.Permission == null) continue;
            var permissions = rule.Permission.Split(',');
            if (!permissions.Contains(Permissions.AppointmentsReadOrganization) && !permissions.Contains(Permissions.SchedulesReadOrganization)) continue;
            Assert.Contains(method.Name, new[] { "ViewOrganizationAppointments", "ViewAppointment", "Get", "GetHistory" });
        }
    }

    private static AppointmentService Appointments(RecordSharingTests.Fixture f) =>
        new(new AppointmentRepository(f.Context), new PractitionerRoleRepository(f.Context), f.Access);
    private static PractitionerScheduleService Schedules(RecordSharingTests.Fixture f) =>
        new(new PractitionerScheduleRepository(f.Context), f.Access);

    private static async Task<(PractitionerRole Second, PractitionerRole Colleague)> Seed(RecordSharingTests.Fixture f)
    {
        var second = new PractitionerRole { OrganizationId = f.ReceivingOrganization.Id, PractitionerId = f.ReceivingRole.PractitionerId, RoleName = "Second assignment" };
        var colleague = new PractitionerRole { OrganizationId = f.ReceivingOrganization.Id, PractitionerId = f.SourceRole.PractitionerId, RoleName = "Colleague" };
        f.Context.AddRange(second, colleague);
        foreach (var role in new[] { f.ReceivingRole, second, colleague, f.SourceRole })
        {
            var session = new PractitionerSchedule { OrganizationId = role.OrganizationId, PractitionerRoleId = role.Id,
                StartTime = new(9, 0), EndTime = new(10, 0), Occurrences = [SessionOccurrence.AllDays] };
            var slot = session.GenerateSlots(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))[0];
            slot.Session = session;
            f.Context.AddRange(session, slot,
                Appointment.Book(f.Patient.Id, slot, f.StaffUserId, null),
                new SessionHistory { OrganizationId = role.OrganizationId, PractitionerScheduleId = session.Id });
        }
        await f.Context.SaveChangesAsync();
        return (second, colleague);
    }
}
