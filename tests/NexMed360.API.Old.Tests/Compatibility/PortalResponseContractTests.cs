using NexMed360Api.Dtos.Search;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexMed360.Infrastructure;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Dtos.practitionerSchedule;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
using Xunit;

namespace NexMed360.API.Old.Tests.Compatibility;

public class PortalResponseContractTests
{
    // Phase 1 examples include the renamed contracts, shared identities, and source organizations.
    // These checks protect the existing portal responses while later workflows are added.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingPortalResponsesMatchExpectedJson(bool additional)
    {
        using var culture = new InvariantCultureScope();
        var database = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=design_only;Username=unused;Password=unused",
            ["Auth:Jwt:Issuer"] = "test", ["Auth:Jwt:Audience"] = "test",
            ["Auth:Jwt:Signing-Key"] = "synthetic-test-key-at-least-32-characters", ["Auth:Jwt:Validity"] = "1"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(); services.AddDataProtection(); services.AddRouting();
        services.AddNexMed360Infrastructure(configuration);
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.AddSingleton(database);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var organizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var patientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var practitionerRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var appointmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var recordId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var organization = new Organization { Id = organizationId, Name = "Synthetic organization", Street = "One street", City = "City", District = "District", Phone = "100", Grade = "A" };
            var patient = new Patient { Id = patientId, PersonId = TestProfiles.PersonId("patient-account"), BirthDate = new DateOnly(2000, 1, 2), Gender = "Test" };
            var practitionerRole = new PractitionerRole { Id = practitionerRoleId, PractitionerId = TestProfiles.PractitionerId("practitionerRole-account"),  OrganizationId = organizationId, Specialty = "General medicine" };
            var organizationMember = new OrganizationMembership { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), UserId = "reception-account",  OrganizationId = organizationId,  };
            var admin = new OrganizationMembership { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), UserId = "admin-account",  OrganizationId = organizationId };
            var users = new[]
            {
                new ApplicationUser { Id = TestProfiles.UserId(patient), FirstName = "Pat", LastName = "Example", Email = "patient@example.test", PhoneNumber = "101", },
                new ApplicationUser { Id = TestProfiles.UserId(practitionerRole), FirstName = "Doc", LastName = "Example", Email = "practitionerRole@example.test", PhoneNumber = "102", },
                new ApplicationUser { Id = organizationMember.UserId, FirstName = "Rec", LastName = "Example", Email = "reception@example.test", PhoneNumber = "103",  },
                new ApplicationUser { Id = admin.UserId, FirstName = "Admin", LastName = "Example", Email = "admin@example.test", PhoneNumber = "104",  }
            };
            context.AddRange(organization, patient, practitionerRole, organizationMember, admin);
            context.Users.AddRange(users);
            foreach (var (user, role) in users.Zip(new[] { UserRoles.PATIENT, UserRoles.PRACTITIONER, UserRoles.ORGANIZATION_MEMBER, UserRoles.ORGANIZATION_MANAGER }))
            {
                context.Roles.Add(new IdentityRole { Id = role, Name = role, NormalizedName = role.ToUpperInvariant() });
                context.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role });
            }
            var session = new PractitionerSchedule { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),  PractitionerRoleId = practitionerRoleId,  OrganizationId = organizationId, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Occurrences = [NexMed360.API.Entities.Enums.SessionOccurrence.AllDays] };
            var slot = new SessionSlot { Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), Session = session, SessionId = session.Id, Date = new DateOnly(2030, 1, 3), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 30), SlotNumber = 1 };
            context.SessionSlots.Add(slot);
            context.Appointments.Add(new Appointment { Id = appointmentId,  PatientId = patientId,  PractitionerRoleId = practitionerRoleId,  OrganizationId = organizationId, CreatedByUserId = organizationMember.UserId,  SessionSlotId = slot.Id, Number = 1, AppointmentDateTime = new DateTime(2030, 1, 3, 9, 0, 0), Remarks = "Synthetic booking" });
            var record = new ClinicalRecord { Id = recordId,  PatientId = patientId,  PractitionerRoleId = practitionerRoleId, VisitDate = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc), Diagnosis = "Synthetic diagnosis", PractitionerNotes = "Synthetic note", FollowUpDate = new DateTime(2026, 2, 2) };
            record.Prescriptions!.Add(new Prescription { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), ClinicalRecord = record, ClinicalRecordId = recordId, MedicineName = "Synthetic medicine", Dosage = "One tablet", TimesPerDay = 2, IsLongTermUse = true, Duration = "7 days", Notes = "Synthetic directions" });
            context.ClinicalRecords.Add(record);
            context.SessionHistories.Add(new SessionHistory { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), PractitionerSchedule = session, PractitionerScheduleId = session.Id, Date = new DateOnly(2026, 1, 2), StartTime = new TimeOnly(9, 5), EndTime = new TimeOnly(9, 45) });
            if (additional)
                context.PractitionerSchedules.Add(new PractitionerSchedule { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), PractitionerRoleId = practitionerRoleId,
                    OrganizationId = organizationId, StartTime = TimeOnly.MinValue, EndTime = TimeOnly.MaxValue, Occurrences = [NexMed360.API.Entities.Enums.SessionOccurrence.AllDays] });
            await TestProfiles.SaveAsync(context);
        }
        async Task<object?> Call<T>(Func<T, Task<object?>> request) where T : notnull
        {
            using var scope = provider.CreateScope();
            return await request(scope.ServiceProvider.GetRequiredService<T>());
        }
        var responses = new SortedDictionary<string, object?>
        {
            ["appointment"] = await Call<IAppointmentService>(async service => await service.Get(appointmentId.ToString(), "patient-account", [UserRoles.PATIENT])),
            ["ownAppointments"] = await Call<IAppointmentService>(async service => await service.GetOwn(new ViewAppointmentsFilterDto(), "practitionerRole-account")),
            ["patient"] = await Call<IPatientService>(async service => await service.GetPatient(patientId.ToString())),
            ["record"] = await Call<IPatientService>(async service => await service.ViewClinicalRecord(recordId.ToString(), "patient-account", false)),
            ["records"] = await Call<IPatientService>(async service => await service.ViewAllClinicalRecords(patientId.ToString(), "patient-account", false)),
            ["organization"] = await Call<IOrganizationService>(async service => await service.FindById(organizationId.ToString(), true)),
            ["members"] = await Call<IOrganizationService>(async service => await service.GetMembers(organizationId.ToString(), "admin-account")),
            ["histories"] = await Call<IPractitionerScheduleService>(async service => await service.GetHistory(new FilterSessionHistoryDto())),
            ["patientAccount"] = await Call<IUserService>(async service => await service.GetUserAsync("patient-account")),
            ["practitionerRoleAccount"] = await Call<IUserService>(async service => await service.GetUserAsync("practitionerRole-account")),
            ["organizationMemberAccount"] = await Call<IUserService>(async service => await service.GetUserAsync("reception-account")),
            ["adminAccount"] = await Call<IUserService>(async service => await service.GetUserAsync("admin-account"))
        };
        if (additional)
        {
            responses = new SortedDictionary<string, object?>
            {
                ["sessions"] = await Call<IPractitionerScheduleService>(async service => await service.Get(new FilterPractitionerScheduleDto { PractitionerRoleId = practitionerRoleId.ToString(), OrganizationId = organizationId.ToString() })),
                ["slots"] = await Call<IPractitionerScheduleService>(async service => await service.GetSlots(new GetSessionSlotsDto { SessionId = Guid.Parse("66666666-6666-6666-6666-666666666666"), Date = new DateOnly(2030, 1, 3) })),
                ["currentSession"] = await Call<IPractitionerScheduleService>(async service => await service.GetCurrentOrUpcoming(new FilterSessionsForCurrentDayDto { PractitionerRoleId = practitionerRoleId.ToString(), OrganizationId = organizationId.ToString(), Date = "2030-01-03" })),
                ["hasSession"] = await Call<IPractitionerScheduleService>(service => Task.FromResult<object?>(service.HasAnySessionToday(new FilterSessionsForCurrentDayDto { PractitionerRoleId = practitionerRoleId.ToString(), OrganizationId = organizationId.ToString(), Date = "2030-01-03" }))),
                ["systemOverview"] = await Call<IOverviewService>(async service => await service.GetSystemOverview()),
                ["searchEmpty"] = await Call<ISearchService>(async service => await service.SearchPractitionerRole(new SearchPractitionerRoleDto())),
                ["searchDenied"] = await Call<ISearchService>(async service => await service.EntitySearch(new EntitySearchDto { Entity = EntityTypes.Patient, InputType = InputTypes.Email, Input = "patient" }, new CallerContext("patient-account", [UserRoles.PATIENT]))),
                ["invalidAppointment"] = await Call<IAppointmentService>(async service => await service.Get("invalid", "patient-account", [UserRoles.PATIENT])),
                ["missingAppointment"] = await Call<IAppointmentService>(async service => await service.Get("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", "patient-account", [UserRoles.PATIENT])),
                ["invalidPatient"] = await Call<IPatientService>(async service => await service.GetPatient("invalid")),
                ["invalidSession"] = await Call<IPractitionerScheduleService>(async service => await service.Start("invalid")),
                ["invalidSessionDelete"] = await Call<IPractitionerScheduleService>(async service => await service.Delete("invalid")),
                ["missingOrganization"] = await Call<IOrganizationService>(async service => await service.FindById("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", false))
            };
        }
        var json = JsonSerializer.Serialize(responses, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var baseline = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Compatibility", additional ? "portal-extra-response-baseline.json" : "portal-response-baseline.json")).Trim();
        Assert.Equal(baseline, json);
    }
    private sealed class InvariantCultureScope : IDisposable
    {
        private readonly CultureInfo previous = CultureInfo.CurrentCulture;
        private readonly CultureInfo previousUi = CultureInfo.CurrentUICulture;
        public InvariantCultureScope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }
        public void Dispose()
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }
}
