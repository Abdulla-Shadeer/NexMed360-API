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
using NexMed360Api.Dtos.doctorSession;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
using Xunit;

namespace NexMed360.API.Old.Tests.Compatibility;

public class PortalResponseContractTests
{
    // Legacy examples with explicit corrections for member/admin details, roles and history doctor fields.
    // Other values and all response field shapes remain frozen.
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
        var hospitalId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var patientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var doctorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var appointmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var recordId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hospital = new Hospital { Id = hospitalId, Name = "Synthetic hospital", Street = "One street", City = "City", District = "District", Phone = "100", Grade = "A" };
            var patient = new Patient { Id = patientId, UserId = "patient-account", BirthDate = new DateOnly(2000, 1, 2), Gender = "Test" };
            var doctor = new Doctor { Id = doctorId, UserId = "doctor-account",  HospitalId = hospitalId, Speciality = "General medicine" };
            var receptionist = new Receptionist { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), UserId = "reception-account",  HospitalId = hospitalId, Department = "Desk", Location = "Floor 1" };
            var admin = new HospitalAdmin { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), UserId = "admin-account",  HospitalId = hospitalId };
            var users = new[]
            {
                new User { Id = patient.UserId, FirstName = "Pat", LastName = "Example", Email = "patient@example.test", PhoneNumber = "101", },
                new User { Id = doctor.UserId, FirstName = "Doc", LastName = "Example", Email = "doctor@example.test", PhoneNumber = "102", },
                new User { Id = receptionist.UserId, FirstName = "Rec", LastName = "Example", Email = "reception@example.test", PhoneNumber = "103",  },
                new User { Id = admin.UserId, FirstName = "Admin", LastName = "Example", Email = "admin@example.test", PhoneNumber = "104",  }
            };
            context.AddRange(hospital, patient, doctor, receptionist, admin);
            context.Users.AddRange(users);
            foreach (var (user, role) in users.Zip(new[] { UserRoles.PATIENT, UserRoles.DOCTOR, UserRoles.RECEPTIONIST, UserRoles.HOSPITAL_ADMIN }))
            {
                context.Roles.Add(new IdentityRole { Id = role, Name = role, NormalizedName = role.ToUpperInvariant() });
                context.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role });
            }
            var session = new DoctorSession { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),  DoctorId = doctorId,  HospitalId = hospitalId, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Occurrences = [NexMed360.API.Entities.Enums.SessionOccurrence.AllDays] };
            var slot = new SessionSlot { Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), Session = session, SessionId = session.Id, Date = new DateOnly(2030, 1, 3), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 30), SlotNumber = 1 };
            context.SessionSlots.Add(slot);
            context.Appointments.Add(new Appointment { Id = appointmentId,  PatientId = patientId,  DoctorId = doctorId,  HospitalId = hospitalId, CreatedByUserId = receptionist.UserId,  SessionSlotId = slot.Id, Number = 1, AppointmentDateTime = new DateTime(2030, 1, 3, 9, 0, 0), Remarks = "Synthetic booking" });
            var record = new ClinicRecord { Id = recordId,  PatientId = patientId,  DoctorId = doctorId, VisitDate = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc), Diagnosis = "Synthetic diagnosis", DoctorNotes = "Synthetic note", FollowUpDate = new DateTime(2026, 2, 2) };
            record.Prescriptions!.Add(new Prescription { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), ClinicRecord = record, ClinicRecordId = recordId, MedicineName = "Synthetic medicine", Dosage = "One tablet", TimesPerDay = 2, IsLongTermUse = true, Duration = "7 days", Notes = "Synthetic directions" });
            context.ClinicRecords.Add(record);
            context.SessionHistories.Add(new SessionHistory { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), DoctorSession = session, DoctorSessionId = session.Id, Date = new DateOnly(2026, 1, 2), StartTime = new TimeOnly(9, 5), EndTime = new TimeOnly(9, 45) });
            if (additional)
                context.DoctorSessions.Add(new DoctorSession { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), DoctorId = doctorId,
                    HospitalId = hospitalId, StartTime = TimeOnly.MinValue, EndTime = TimeOnly.MaxValue, Occurrences = [NexMed360.API.Entities.Enums.SessionOccurrence.AllDays] });
            await context.SaveChangesAsync();
        }
        async Task<object?> Call<T>(Func<T, Task<object?>> request) where T : notnull
        {
            using var scope = provider.CreateScope();
            return await request(scope.ServiceProvider.GetRequiredService<T>());
        }
        var responses = new SortedDictionary<string, object?>
        {
            ["appointment"] = await Call<IAppointmentService>(async service => await service.Get(appointmentId.ToString(), "patient-account", [UserRoles.PATIENT])),
            ["ownAppointments"] = await Call<IAppointmentService>(async service => await service.GetOwn(new ViewAppointmentsFilterDto(), "doctor-account")),
            ["patient"] = await Call<IPatientService>(async service => await service.GetPatient(patientId.ToString())),
            ["record"] = await Call<IPatientService>(async service => await service.ViewClinicRecord(recordId.ToString(), "patient-account", false)),
            ["records"] = await Call<IPatientService>(async service => await service.ViewAllClinicRecords(patientId.ToString(), "patient-account", false)),
            ["hospital"] = await Call<IHospitalService>(async service => await service.FindById(hospitalId.ToString(), true)),
            ["members"] = await Call<IHospitalService>(async service => await service.GetMembers(hospitalId.ToString(), "admin-account")),
            ["histories"] = await Call<IDoctorSessionService>(async service => await service.GetHistory(new FilterSessionHistoryDto())),
            ["patientAccount"] = await Call<IUserService>(async service => await service.GetUserAsync("patient-account")),
            ["doctorAccount"] = await Call<IUserService>(async service => await service.GetUserAsync("doctor-account")),
            ["receptionistAccount"] = await Call<IUserService>(async service => await service.GetUserAsync("reception-account")),
            ["adminAccount"] = await Call<IUserService>(async service => await service.GetUserAsync("admin-account"))
        };
        if (additional)
        {
            responses = new SortedDictionary<string, object?>
            {
                ["sessions"] = await Call<IDoctorSessionService>(async service => await service.Get(new FilterDoctorSessionDto { DoctorId = doctorId.ToString(), HospitalId = hospitalId.ToString() })),
                ["slots"] = await Call<IDoctorSessionService>(async service => await service.GetSlots(new GetSessionSlotsDto { SessionId = Guid.Parse("66666666-6666-6666-6666-666666666666"), Date = new DateOnly(2030, 1, 3) })),
                ["currentSession"] = await Call<IDoctorSessionService>(async service => await service.GetCurrentOrUpcoming(new FilterSessionsForCurrentDayDto { DoctorId = doctorId.ToString(), HospitalId = hospitalId.ToString(), Date = "2030-01-03" })),
                ["hasSession"] = await Call<IDoctorSessionService>(service => Task.FromResult<object?>(service.HasAnySessionToday(new FilterSessionsForCurrentDayDto { DoctorId = doctorId.ToString(), HospitalId = hospitalId.ToString(), Date = "2030-01-03" }))),
                ["systemOverview"] = await Call<IOverviewService>(async service => await service.GetSystemOverview()),
                ["searchEmpty"] = await Call<ISearchService>(async service => await service.SearchDoctor(new SearchDoctorDto())),
                ["searchDenied"] = await Call<ISearchService>(async service => await service.EntitySearch(new EntitySearchDto { Entity = EntityTypes.Patient, InputType = InputTypes.Email, Input = "patient" }, new CallerContext("patient-account", [UserRoles.PATIENT]))),
                ["invalidAppointment"] = await Call<IAppointmentService>(async service => await service.Get("invalid", "patient-account", [UserRoles.PATIENT])),
                ["missingAppointment"] = await Call<IAppointmentService>(async service => await service.Get("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", "patient-account", [UserRoles.PATIENT])),
                ["invalidPatient"] = await Call<IPatientService>(async service => await service.GetPatient("invalid")),
                ["invalidSession"] = await Call<IDoctorSessionService>(async service => await service.Start("invalid")),
                ["invalidSessionDelete"] = await Call<IDoctorSessionService>(async service => await service.Delete("invalid")),
                ["missingHospital"] = await Call<IHospitalService>(async service => await service.FindById("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", false))
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
