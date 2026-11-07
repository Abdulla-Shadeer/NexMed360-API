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
using NexMed360Api.Dtos.doctorSession;
using NexMed360Api.Dtos.User;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.API.Old.Tests.Compatibility;

public class PortalProjectionTests
{
    [Fact]
    public async Task MembersContainStoredAccountRolesAndHospitalDetailsForEachProfile()
    {
        using var fixture = await Fixture.Create();
        var result = await fixture.Call<IHospitalService, NexMed360Api.Common.Result<List<ViewUserDto>>>(
            service => service.GetMembers(fixture.HospitalId.ToString(), "admin"));
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        var doctor = Assert.Single(result.Data, row => row.DoctorProfile != null);
        AssertAccount(doctor, "doctor", "Doc", "201");
        Assert.Equal(new[] { UserRoles.DOCTOR, UserRoles.PATIENT }.Order(), doctor.Roles.Order());
        var profile = doctor.DoctorProfile!;
        Assert.Equal(fixture.DoctorId.ToString(), profile.Id);
        Assert.Equal(doctor.Id, profile.UserId);
        Assert.Equal("Doc", profile.FirstName);
        Assert.Equal("Example", profile.LastName);
        Assert.Equal("doctor@example.test", profile.Email);
        Assert.Equal("201", profile.PhoneNumber);
        Assert.Equal("General medicine", profile.Speciality);
        Assert.Equal(fixture.HospitalId.ToString(), profile.HospitalId);
        Assert.Equal("Main hospital", profile.HospitalName);
        // Membership results keep only the requested member profiles.
        Assert.Null(doctor.PatientProfile);
        var receptionist = Assert.Single(result.Data, row => row.ReceptionistProfile != null);
        AssertAccount(receptionist, "receptionist", "Rec", "202");
        Assert.Equal(new[] { UserRoles.RECEPTIONIST }, receptionist.Roles);
        var reception = receptionist.ReceptionistProfile!;
        Assert.Equal(receptionist.Id, reception.UserId);
        Assert.Equal("Rec", reception.FirstName);
        Assert.Equal("Example", reception.LastName);
        Assert.Equal("receptionist@example.test", reception.Email);
        Assert.Equal("202", reception.PhoneNumber);
        Assert.Equal("Main hospital", reception.HospitalName);
        Assert.Equal(fixture.HospitalId.ToString(), reception.HospitalId);
        Assert.Equal("OPD", reception.Department);
        Assert.Equal("Front office", reception.Location);
        Assert.DoesNotContain(result.Data, row => row.Id == "other-doctor");
    }

    [Fact]
    public async Task MemberDetailsRemainUnavailableToAnUnrelatedHospitalAdmin()
    {
        using var fixture = await Fixture.Create();
        var result = await fixture.Call<IHospitalService, NexMed360Api.Common.Result<List<ViewUserDto>>>(
            service => service.GetMembers(fixture.HospitalId.ToString(), "other-admin"));
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(401, result.Error!.HttpStatus);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("hospital")]
    [InlineData("assignedProfile")]
    public async Task AdministratorDetailsAreLoadedInEveryExistingProfileProjection(string source)
    {
        using var fixture = await Fixture.Create();
        var profile = source switch
        {
            "account" => (await fixture.Call<IUserService, NexMed360Api.Common.Result<ViewUserDto>>(service => service.GetUserAsync("admin"))).Data!.HospitalAdminProfile!,
            "hospital" => (await fixture.Call<IHospitalService, NexMed360Api.Common.Result<NexMed360Api.Dtos.Hospital.ViewHospitalDto>>(
                service => service.FindById(fixture.HospitalId.ToString(), true))).Data!.Admins!.Single().HospitalAdminProfile!,
            _ => (await fixture.Call<PortalViews, NexMed360Api.Dtos.HospitalAdmin.ViewHospitalAdminDto?>(views =>
                views.Admin(new HospitalAdmin { HospitalId = fixture.HospitalId, UserId = "admin" })))!
        };
        Assert.Equal("admin", profile.UserId);
        Assert.Equal("Admin", profile.FirstName);
        Assert.Equal("Example", profile.LastName);
        Assert.Equal("admin@example.test", profile.Email);
        Assert.Equal("203", profile.PhoneNumber);
        Assert.Equal("Main hospital", profile.HospitalName);
    }

    [Fact]
    public async Task HospitalAdminListLoadsRolesWithoutExpandingItsExistingVisibility()
    {
        using var fixture = await Fixture.Create();
        var visible = await fixture.Call<IHospitalService, NexMed360Api.Common.Result<NexMed360Api.Dtos.Hospital.ViewHospitalDto>>(
            service => service.FindById(fixture.HospitalId.ToString(), true));
        var admin = Assert.Single(visible.Data!.Admins!);
        AssertAccount(admin, "admin", "Admin", "203");
        Assert.Equal(new[] { UserRoles.HOSPITAL_ADMIN }, admin.Roles);
        var hidden = await fixture.Call<IHospitalService, NexMed360Api.Common.Result<NexMed360Api.Dtos.Hospital.ViewHospitalDto>>(
            service => service.FindById(fixture.HospitalId.ToString(), false));
        Assert.Empty(hidden.Data!.Admins!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoryAndOngoingResponsesContainTheAssociatedDoctorsDetails(bool ongoing)
    {
        using var fixture = await Fixture.Create();
        var result = await fixture.Call<IDoctorSessionService, NexMed360Api.Common.Result<List<ViewSessionHistoryDto>>>(
            service => ongoing ? service.GetOngoing("receptionist") : service.GetHistory(new FilterSessionHistoryDto { SessionId = fixture.SessionId.ToString() }));
        Assert.True(result.IsSuccess);
        var history = Assert.Single(result.Data!);
        Assert.Equal("Doc Example", history.DoctorName);
        Assert.Equal("General medicine", history.DoctorSpeciality);
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
        Assert.Equal("Doc Example", appointment.Data.DoctorName);
        Assert.Equal("Rec Example", appointment.Data.CreatedByUserName);
        Assert.Equal("Main hospital", appointment.Data.HospitalName);
        var record = await fixture.Call<IPatientService, NexMed360Api.Common.Result<NexMed360Api.Dtos.ClinicRecord.ViewClinicRecordDto>>(
            service => service.ViewClinicRecord(fixture.RecordId.ToString(), "patient", false));
        Assert.Equal("Pat Example", record.Data!.PatientName);
        Assert.Equal("Doc Example", record.Data.DoctorName);
        var prescription = Assert.Single(record.Data.Prescriptions!);
        Assert.Equal("Doc Example", prescription.IssuedByDoctor);
        Assert.Equal("Main hospital", prescription.IssuedAtHospital);
        // No originating-hospital key exists on ClinicRecord yet; do not invent one from the doctor's current assignment.
        Assert.Null(record.Data.HospitalName);
        var session = await fixture.Call<IDoctorSessionService, NexMed360Api.Common.Result<List<ViewDoctorSessionDto>>>(
            service => service.Get(new FilterDoctorSessionDto { DoctorId = fixture.DoctorId.ToString() }));
        Assert.Equal("Main hospital", Assert.Single(session.Data!).HospitalName);
        // SearchDoctor uses this same batch projection after its PostgreSQL search query.
        var doctors = await fixture.Call<PortalViews, List<NexMed360Api.Dtos.Doctor.ViewDoctorDto>>(views => views.Doctors(
            [new Doctor { Id = fixture.DoctorId, UserId = "doctor", HospitalId = fixture.HospitalId, Speciality = "General medicine" }]));
        Assert.Equal("Doc", Assert.Single(doctors).FirstName);
        Assert.Equal("Main hospital", doctors[0].HospitalName);
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
        public Guid HospitalId { get; } = Guid.NewGuid();
        public Guid DoctorId { get; } = Guid.NewGuid();
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
            var hospital = new Hospital { Id = fixture.HospitalId, Name = "Main hospital", Street = "One", City = "City", District = "District", Phone = "100", Grade = "A" };
            var otherHospital = new Hospital { Name = "Other hospital", Street = "Two", City = "Other city", District = "Other district", Phone = "200", Grade = "B" };
            var doctor = new Doctor { Id = fixture.DoctorId, UserId = "doctor", HospitalId = hospital.Id, Speciality = "General medicine" };
            var session = new DoctorSession { Id = fixture.SessionId, DoctorId = doctor.Id, HospitalId = hospital.Id,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Occurrences = [NexMed360.API.Entities.Enums.SessionOccurrence.AllDays] };
            var slot = new SessionSlot { Session = session, SessionId = session.Id, Date = DateOnly.FromDateTime(DateTime.Today),
                StartTime = session.StartTime, EndTime = session.EndTime, SlotNumber = 7 };
            var record = new ClinicRecord { Id = fixture.RecordId, PatientId = fixture.PatientId, DoctorId = doctor.Id, Diagnosis = "Synthetic diagnosis", VisitDate = DateTime.UtcNow };
            record.Prescriptions!.Add(new Prescription { ClinicRecord = record, ClinicRecordId = record.Id, MedicineName = "Synthetic medicine", Dosage = "One", Duration = "7 days" });
            context.AddRange(hospital, otherHospital, doctor, session, slot, record,
                new Patient { Id = fixture.PatientId, UserId = "patient", BirthDate = new DateOnly(2000, 1, 1), Gender = "Test" },
                new HospitalAdmin { UserId = "admin", HospitalId = hospital.Id },
                new HospitalAdmin { UserId = "other-admin", HospitalId = otherHospital.Id },
                new Receptionist { UserId = "receptionist", HospitalId = hospital.Id, Department = "OPD", Location = "Front office" },
                new Doctor { UserId = "other-doctor", HospitalId = otherHospital.Id, Speciality = "Other specialty" },
                new Appointment { Id = fixture.AppointmentId, PatientId = fixture.PatientId, DoctorId = doctor.Id,
                    HospitalId = hospital.Id, SessionSlotId = slot.Id, CreatedByUserId = "receptionist", AppointmentDateTime = DateTime.Today.AddHours(9) },
                new SessionHistory { DoctorSession = session, DoctorSessionId = session.Id, Date = DateOnly.FromDateTime(DateTime.Today),
                    StartTime = new TimeOnly(9, 5), SessionStatus = SessionStatus.Started });
            foreach (var (id, firstName, phone, roles) in new[]
            {
                ("doctor", "Doc", "201", new[] { UserRoles.DOCTOR, UserRoles.PATIENT }),
                ("receptionist", "Rec", "202", new[] { UserRoles.RECEPTIONIST }),
                ("admin", "Admin", "203", new[] { UserRoles.HOSPITAL_ADMIN }),
                ("patient", "Pat", "204", new[] { UserRoles.PATIENT }),
                ("other-doctor", "Other", "205", new[] { UserRoles.DOCTOR }),
                ("other-admin", "Other", "206", new[] { UserRoles.HOSPITAL_ADMIN })
            })
            {
                context.Users.Add(new User { Id = id, FirstName = firstName, LastName = "Example", Email = $"{id}@example.test", PhoneNumber = phone });
                foreach (var role in roles) context.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = role });
            }
            foreach (var role in new[] { UserRoles.DOCTOR, UserRoles.PATIENT, UserRoles.HOSPITAL_ADMIN, UserRoles.RECEPTIONIST })
                context.Roles.Add(new IdentityRole { Id = role, Name = role, NormalizedName = role.ToUpperInvariant() });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            return fixture;
        }
        public void Dispose() => provider.Dispose();
    }
}
