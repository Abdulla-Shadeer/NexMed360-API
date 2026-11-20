using System.Data.Common;
using NexMed360.Application.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NexMed360.Infrastructure.Repositories;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Entities;

namespace NexMed360.API.Old.Tests;

public class RepositoryQueryTranslationTests
{
    [Theory]
    [InlineData("Members")]
    [InlineData("HasPermission")]
    [InlineData("MemberRoles")]
    [InlineData("SelectedStaffRoles")]
    [InlineData("Appointment")]
    [InlineData("ClinicalRecord")]
    [InlineData("PractitionerRoles")]
    [InlineData("ActivePractitionerAssignment")]
    [InlineData("OwnSchedules")]
    [InlineData("OwnAppointment")]
    [InlineData("PatientSearch")]
    [InlineData("SharedClinicalRecord")]
    [InlineData("PatientSharingRequests")]
    [InlineData("OrganizationSharingRequests")]
    [InlineData("PatientPortalAccess")]
    public async Task RepositoryJoinsTranslateToPostgreSqlBeforeOpeningAConnection(string query)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused")
            .AddInterceptors(new StopBeforeConnecting()).Options;
        var access = new WorkspaceAccess();
        if (query is "SharedClinicalRecord" or "OrganizationSharingRequests" or "OwnSchedules" or "OwnAppointment")
            access.Select(new() { Mode = WorkspaceModes.Work, OrganizationId = Guid.NewGuid(), LocationIds = [Guid.NewGuid()], PractitionerRoleIds = [Guid.NewGuid()], Permissions = [Permissions.ClinicalRecordsRead, Permissions.SchedulesReadOwn, Permissions.AppointmentsReadOwn] });
        using var context = new ApplicationDbContext(options, access);
        var id = Guid.NewGuid();

        // Execute the real repository query. Translation errors occur before connection opening.
        Task RunQuery() => query switch
        {
            "Members" => new AccessRepository(context).Members(),
            "HasPermission" => new OrganizationRepository(context).HasPermission(id, "staff", Permissions.MembersManage),
            "MemberRoles" => new AccessRepository(context).MemberRoles(id, Guid.NewGuid()),
            "SelectedStaffRoles" => new OrganizationMemberRepository(context).FindRoles(id, [Guid.NewGuid()]),
            "Appointment" => new AppointmentRepository(context).FindReadable(id, "staff", [Permissions.EncountersStart]),
            "ClinicalRecord" => new ClinicalRecordRepository(context).FindReadable(id, "patient", false),
            "OwnSchedules" => new PractitionerScheduleRepository(context).Get(new(null, null, null, null, true)),
            "OwnAppointment" => new AppointmentRepository(context).FindReadable(id, "staff", [Permissions.AppointmentsReadOwn]),
            "ActivePractitionerAssignment" => new PractitionerRoleRepository(context).FindActiveForUser(id, "staff"),
            "PractitionerRoles" => new PractitionerRoleRepository(context).Views([new PractitionerRole { Id = id }]),
            "SharedClinicalRecord" => new ClinicalRecordRepository(context).FindReadable(id, "staff", true),
            "PatientSharingRequests" => new RecordSharingRepository(context).ForPatient(id),
            "OrganizationSharingRequests" => new RecordSharingRepository(context).ForOrganization(access.OrganizationId!.Value, id),
            "PatientPortalAccess" => new RecordSharingRepository(context).PatientHasPortalAccess(id),
            "PatientSearch" => new SearchRepository(context, null!, null!).Patients(InputTypes.Email, "patient"),
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };

        await Assert.ThrowsAsync<ConnectionOpeningStopped>(RunQuery);
    }

    private sealed class ConnectionOpeningStopped : Exception;

    private sealed class StopBeforeConnecting : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new ConnectionOpeningStopped();
    }
}
