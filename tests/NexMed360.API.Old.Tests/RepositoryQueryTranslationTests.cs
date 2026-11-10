using System.Data.Common;
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
    [InlineData("Appointment")]
    [InlineData("ClinicalRecord")]
    [InlineData("PractitionerRoles")]
    [InlineData("PatientSearch")]
    public async Task RepositoryJoinsTranslateToPostgreSqlBeforeOpeningAConnection(string query)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused")
            .AddInterceptors(new StopBeforeConnecting()).Options;
        using var context = new ApplicationDbContext(options);
        var id = Guid.NewGuid();

        // Execute the real repository query. Translation errors occur before connection opening.
        Task RunQuery() => query switch
        {
            "Members" => new AccessRepository(context).Members(),
            "HasPermission" => new OrganizationRepository(context).HasPermission(id, "staff", Permissions.MembersManage),
            "MemberRoles" => new AccessRepository(context).MemberRoles(id, Guid.NewGuid()),
            "Appointment" => new AppointmentRepository(context).FindReadable(id, "staff", [Permissions.EncountersStart]),
            "ClinicalRecord" => new ClinicalRecordRepository(context).FindReadable(id, "patient", false),
            "PractitionerRoles" => new PractitionerRoleRepository(context).Views([new PractitionerRole { Id = id }]),
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
