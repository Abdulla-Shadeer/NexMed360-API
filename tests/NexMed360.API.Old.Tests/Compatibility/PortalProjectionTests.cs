using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexMed360.Application.Shared;
using NexMed360.Infrastructure;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Dtos.practitionerSchedule;
using NexMed360Api.Dtos.User;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests.Compatibility;

public class PortalProjectionTests
{
    [Fact]
    public async Task MembersContainStoredAccountRolesAndOrganizationDetailsForEachProfile()
    {
        using var fixture = await Fixture.Create();
        var result = await fixture.Call<IOrganizationService, NexMed360Api.Common.Result<List<ViewUserDto>>>(
            service => service.GetMembers(fixture.OrganizationId.ToString(), "admin"));
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Count);
        var practitionerRole = Assert.Single(result.Data, row => row.PractitionerRoleProfile != null);
        AssertAccount(practitionerRole, "practitionerRole", "Doc", "201");
        Assert.Equal(new[] { UserRoles.PRACTITIONER, UserRoles.PATIENT }.Order(), practitionerRole.Roles.Order());
        var profile = practitionerRole.PractitionerRoleProfile!;
        Assert.Equal(fixture.PractitionerRoleId.ToString(), profile.Id);
        Assert.Equal(practitionerRole.Id, profile.UserId);
        Assert.Equal("Doc", profile.FirstName);
        Assert.Equal("Example", profile.LastName);
        Assert.Equal("practitionerRole@example.test", profile.Email);
        Assert.Equal("201", profile.PhoneNumber);
        Assert.Equal("General medicine", profile.Specialty);
        Assert.Equal(fixture.OrganizationId.ToString(), profile.OrganizationId);
        Assert.Equal("Main organization", profile.OrganizationName);
        // Membership results keep only the requested member profiles.
        Assert.Null(practitionerRole.PatientProfile);
        var organizationMember = Assert.Single(result.Data, row => row.Id == "organizationMember");
        AssertAccount(organizationMember, "organizationMember", "Rec", "202");
        Assert.Equal(new[] { UserRoles.ORGANIZATION_MEMBER }, organizationMember.Roles);
        var reception = organizationMember.OrganizationMemberProfile!;
        Assert.Equal(organizationMember.Id, reception.UserId);
        Assert.Equal("Rec", reception.FirstName);
        Assert.Equal("Example", reception.LastName);
        Assert.Equal("organizationMember@example.test", reception.Email);
        Assert.Equal("202", reception.PhoneNumber);
        Assert.Equal("Main organization", reception.OrganizationName);
        Assert.Equal(fixture.OrganizationId.ToString(), reception.OrganizationId);
        Assert.Null(reception.Department);
        Assert.Null(reception.Location);
        Assert.DoesNotContain(result.Data, row => row.Id == "other-practitionerRole");
    }

    [Fact]
    public async Task MemberDetailsRemainUnavailableToAnUnrelatedOrganizationManager()
    {
        using var fixture = await Fixture.Create();
        var result = await fixture.Call<IOrganizationService, NexMed360Api.Common.Result<List<ViewUserDto>>>(
            service => service.GetMembers(fixture.OrganizationId.ToString(), "other-admin"));
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(401, result.Error!.HttpStatus);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("organization")]
    [InlineData("assignedProfile")]
    public async Task AdministratorDetailsAreLoadedInEveryExistingProfileProjection(string source)
    {
        using var fixture = await Fixture.Create();
        var profile = source switch
        {
            "account" => (await fixture.Call<IUserService, NexMed360Api.Common.Result<ViewUserDto>>(service => service.GetUserAsync("admin"))).Data!.OrganizationManagerProfile!,
            "organization" => (await fixture.Call<IOrganizationService, NexMed360Api.Common.Result<NexMed360Api.Dtos.Organization.ViewOrganizationDto>>(
                service => service.FindById(fixture.OrganizationId.ToString(), true))).Data!.Admins!.Single().OrganizationManagerProfile!,
            _ => (await fixture.Call<PortalViews, NexMed360Api.Dtos.OrganizationMember.ViewOrganizationManagerDto?>(views =>
                views.Admin(new OrganizationMembership { OrganizationId = fixture.OrganizationId, UserId = "admin" })))!
        };
        Assert.Equal("admin", profile.UserId);
        Assert.Equal("Admin", profile.FirstName);
        Assert.Equal("Example", profile.LastName);
        Assert.Equal("admin@example.test", profile.Email);
        Assert.Equal("203", profile.PhoneNumber);
        Assert.Equal("Main organization", profile.OrganizationName);
    }

    [Fact]
    public async Task OrganizationManagerListLoadsRolesWithoutExpandingItsExistingVisibility()
    {
        using var fixture = await Fixture.Create();
        var visible = await fixture.Call<IOrganizationService, NexMed360Api.Common.Result<NexMed360Api.Dtos.Organization.ViewOrganizationDto>>(
            service => service.FindById(fixture.OrganizationId.ToString(), true));
        var admin = Assert.Single(visible.Data!.Admins!);
        AssertAccount(admin, "admin", "Admin", "203");
        Assert.Equal(new[] { UserRoles.ORGANIZATION_MANAGER }, admin.Roles);
        var hidden = await fixture.Call<IOrganizationService, NexMed360Api.Common.Result<NexMed360Api.Dtos.Organization.ViewOrganizationDto>>(
            service => service.FindById(fixture.OrganizationId.ToString(), false));
        Assert.Empty(hidden.Data!.Admins!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoryAndOngoingResponsesContainTheAssociatedPractitionerRolesDetails(bool ongoing)
    {
        using var fixture = await Fixture.Create();
        var result = await fixture.Call<IPractitionerScheduleService, NexMed360Api.Common.Result<List<ViewSessionHistoryDto>>>(
            service => ongoing ? service.GetOngoing("organizationMember") : service.GetHistory(new FilterSessionHistoryDto { SessionId = fixture.SessionId.ToString() }));
        Assert.True(result.IsSuccess);
        var history = Assert.Single(result.Data!);
        Assert.Equal("Doc Example", history.PractitionerName);
        Assert.Equal("General medicine", history.PractitionerSpecialty);
        Assert.Equal(new TimeOnly(9, 0), history.StartTime);
        Assert.Equal(new TimeOnly(10, 0), history.EndTime);
    }

    [Fact]
    public async Task OtherSharedProjectionsLoadDetailsFromFreshScopes()
    {
        using var fixture = await Fixture.Create();
        var patient = await fixture.Call<IPatientService, NexMed360Api.Common.Result<NexMed360Api.Dtos.Patient.ViewPatientDto>>(
            service => service.GetPatient(fixture.PatientId.ToString()));
        Assert.Equal("Pat", patient.Data!.FirstName);
        Assert.Equal("patient@example.test", patient.Data.Email);
        var appointment = await fixture.Call<IAppointmentService, NexMed360Api.Common.Result<ViewAppointmentDto>>(
            service => service.Get(fixture.AppointmentId.ToString(), "patient", [UserRoles.PATIENT]));
        Assert.Equal("Pat Example", appointment.Data!.PatientName);
        Assert.Equal("Doc Example", appointment.Data.PractitionerName);
        Assert.Equal("Rec Example", appointment.Data.CreatedByUserName);
        Assert.Equal("Main organization", appointment.Data.OrganizationName);
        var record = await fixture.Call<IPatientService, NexMed360Api.Common.Result<NexMed360Api.Dtos.ClinicalRecord.ViewClinicalRecordDto>>(
            service => service.ViewClinicalRecord(fixture.RecordId.ToString(), "patient", false));
        Assert.Equal("Pat Example", record.Data!.PatientName);
        Assert.Equal("Doc Example", record.Data.PractitionerName);
        var prescription = Assert.Single(record.Data.Prescriptions!);
        Assert.Equal("Doc Example", prescription.IssuedByPractitioner);
        Assert.Equal("Main organization", prescription.IssuedAtOrganization);
        // Use the stored source organization for historical records.
        Assert.Equal("Main organization", record.Data.OrganizationName);
        var session = await fixture.Call<IPractitionerScheduleService, NexMed360Api.Common.Result<List<ViewPractitionerScheduleDto>>>(
            service => service.Get(new FilterPractitionerScheduleDto { PractitionerRoleId = fixture.PractitionerRoleId.ToString() }));
        Assert.Equal("Main organization", Assert.Single(session.Data!).OrganizationName);
        // SearchPractitionerRole uses this same batch projection after its PostgreSQL search query.
        var practitionerRoles = await fixture.Call<PortalViews, List<NexMed360Api.Dtos.PractitionerRole.ViewPractitionerRoleDto>>(views => views.PractitionerRoles(
            [new PractitionerRole { Id = fixture.PractitionerRoleId, PractitionerId = TestProfiles.PractitionerId("practitionerRole"), OrganizationId = fixture.OrganizationId, Specialty = "General medicine" }]));
        Assert.Equal("Doc", Assert.Single(practitionerRoles).FirstName);
        Assert.Equal("Main organization", practitionerRoles[0].OrganizationName);
    }

    private static void AssertAccount(ViewUserDto account, string id, string firstName, string phone)
    {
        Assert.Equal(id, account.Id);
        Assert.Equal(firstName, account.FirstName);
        Assert.Equal("Example", account.LastName);
        Assert.Equal($"{id}@example.test", account.Email);
        Assert.Equal(phone, account.PhoneNumber);
    }

    private sealed class Fixture(ServiceProvider provider) : IDisposable
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public Guid PractitionerRoleId { get; } = Guid.NewGuid();
        public Guid PatientId { get; } = Guid.NewGuid();
        public Guid SessionId { get; } = Guid.NewGuid();
        public Guid AppointmentId { get; } = Guid.NewGuid();
        public Guid RecordId { get; } = Guid.NewGuid();
        public async Task<TResult> Call<TService, TResult>(Func<TService, Task<TResult>> request) where TService : notnull
        {
            using var scope = provider.CreateScope();
            return await request(scope.ServiceProvider.GetRequiredService<TService>());
        }
        public static async Task<Fixture> Create()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused",
                ["Auth:Jwt:Issuer"] = "test", ["Auth:Jwt:Audience"] = "test",
                ["Auth:Jwt:Signing-Key"] = "synthetic-projection-key-at-least-32-characters", ["Auth:Jwt:Validity"] = "1"
            }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging(); services.AddDataProtection(); services.AddRouting();
            services.AddNexMed360Infrastructure(config);
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddSingleton(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            var fixture = new Fixture(provider);
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var organization = new Organization { Id = fixture.OrganizationId, Name = "Main organization", Street = "One", City = "City", District = "District", Phone = "100", Grade = "A" };
            var otherOrganization = new Organization { Name = "Other organization", Street = "Two", City = "Other city", District = "Other district", Phone = "200", Grade = "B" };
            var practitionerRole = new PractitionerRole { Id = fixture.PractitionerRoleId, PractitionerId = TestProfiles.PractitionerId("practitionerRole"), OrganizationId = organization.Id, Specialty = "General medicine" };
            var session = new PractitionerSchedule { Id = fixture.SessionId, PractitionerRoleId = practitionerRole.Id, OrganizationId = organization.Id,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Occurrences = [NexMed360.API.Entities.Enums.SessionOccurrence.AllDays] };
            var slot = new SessionSlot { Session = session, SessionId = session.Id, Date = DateOnly.FromDateTime(DateTime.Today),
                StartTime = session.StartTime, EndTime = session.EndTime, SlotNumber = 7 };
            var record = new ClinicalRecord { Id = fixture.RecordId, PatientId = fixture.PatientId, PractitionerRoleId = practitionerRole.Id, Diagnosis = "Synthetic diagnosis", VisitDate = DateTime.UtcNow };
            record.Prescriptions!.Add(new Prescription { ClinicalRecord = record, ClinicalRecordId = record.Id, MedicineName = "Synthetic medicine", Dosage = "One", Duration = "7 days" });
            context.AddRange(organization, otherOrganization, practitionerRole, session, slot, record,
                new Patient { Id = fixture.PatientId, PersonId = TestProfiles.PersonId("patient"), BirthDate = new DateOnly(2000, 1, 1), Gender = "Test" },
                new OrganizationMembership { UserId = "admin", OrganizationId = organization.Id },
                new OrganizationMembership { UserId = "other-admin", OrganizationId = otherOrganization.Id },
                new OrganizationMembership { UserId = "organizationMember", OrganizationId = organization.Id,  },
                new PractitionerRole { PractitionerId = TestProfiles.PractitionerId("other-practitionerRole"), OrganizationId = otherOrganization.Id, Specialty = "Other specialty" },
                new Appointment { Id = fixture.AppointmentId, PatientId = fixture.PatientId, PractitionerRoleId = practitionerRole.Id,
                    OrganizationId = organization.Id, SessionSlotId = slot.Id, CreatedByUserId = "organizationMember", AppointmentDateTime = DateTime.Today.AddHours(9) },
                new SessionHistory { PractitionerSchedule = session, PractitionerScheduleId = session.Id, Date = DateOnly.FromDateTime(DateTime.Today),
                    StartTime = new TimeOnly(9, 5), SessionStatus = SessionStatus.Started });
            foreach (var (id, firstName, phone, roles) in new[]
            {
                ("practitionerRole", "Doc", "201", new[] { UserRoles.PRACTITIONER, UserRoles.PATIENT }),
                ("organizationMember", "Rec", "202", new[] { UserRoles.ORGANIZATION_MEMBER }),
                ("admin", "Admin", "203", new[] { UserRoles.ORGANIZATION_MANAGER }),
                ("patient", "Pat", "204", new[] { UserRoles.PATIENT }),
                ("other-practitionerRole", "Other", "205", new[] { UserRoles.PRACTITIONER }),
                ("other-admin", "Other", "206", new[] { UserRoles.ORGANIZATION_MANAGER })
            })
            {
                context.Users.Add(new ApplicationUser { Id = id, FirstName = firstName, LastName = "Example", Email = $"{id}@example.test", PhoneNumber = phone });
                foreach (var role in roles) context.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = role });
            }
            foreach (var role in new[] { UserRoles.PRACTITIONER, UserRoles.PATIENT, UserRoles.ORGANIZATION_MANAGER, UserRoles.ORGANIZATION_MEMBER })
                context.Roles.Add(new IdentityRole { Id = role, Name = role, NormalizedName = role.ToUpperInvariant() });
            await TestProfiles.SaveAsync(context, TestContext.Current.CancellationToken);
            return fixture;
        }
        public void Dispose() => provider.Dispose();
    }
}
