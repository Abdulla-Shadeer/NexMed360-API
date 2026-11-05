using Microsoft.EntityFrameworkCore;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Services;
using BookingFixture = NexMed360.API.Old.Tests.AppointmentBookingEligibilityTests.Fixture;

namespace NexMed360.API.Old.Tests;

public class AppointmentReadAccessTests
{
    [Theory]
    [InlineData(UserRoles.PATIENT)]
    [InlineData(UserRoles.DOCTOR)]
    public async Task PatientAndAssignedDoctorCanReadTheAppointment(string role)
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = role == UserRoles.PATIENT
            ? (await fixture.Context.Patients.SingleAsync()).UserId
            : (await fixture.Context.Doctors.SingleAsync()).UserId;
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.Get(id, caller, [role]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(id, result.Data.BookingReference);
        Assert.Equal(fixture.PatientId.ToString(), result.Data.PatientId);
        Assert.Equal("Synthetic Patient", result.Data.PatientName);
        Assert.Equal("Synthetic Doctor", result.Data.DoctorName);
        Assert.Equal("Synthetic hospital", result.Data.HospitalName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReceptionistAccessIsLimitedToTheAppointmentsHospital(bool sameHospital)
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = await AddReceptionist(fixture, sameHospital);

        var result = await fixture.Service.Get(id, caller, [UserRoles.RECEPTIONIST]);

        Assert.Equal(sameHospital, result.IsSuccess);
        if (sameHospital)
            Assert.Equal(id, result.Data!.BookingReference);
        else
        {
            Assert.Null(result.Data);
            Assert.Equal(404, result.Error!.HttpStatus);
        }
    }

    [Fact]
    public async Task AnotherDoctorAtTheSameHospitalCannotReadAnUnassignedAppointment()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var hospital = await fixture.Context.Hospitals.SingleAsync();
        var user = new User { Id = "other-doctor", FirstName = "Other", LastName = "Doctor" };
        fixture.Context.Doctors.Add(new Doctor
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user,
            HospitalId = hospital.Id, Hospital = hospital, Speciality = "Test speciality"
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.Get(id, user.Id, [UserRoles.DOCTOR]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task UnrelatedPatientOrCreatorCannotReadAndGetsTheMissingTargetResponse()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var unrelated = new Patient
        {
            Id = Guid.NewGuid(), UserId = fixture.RequesterId, Gender = "Test",
            BirthDate = new DateOnly(2000, 1, 1)
        };
        fixture.Context.Patients.Add(unrelated);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        // This caller created the booking but is not its patient, doctor or receptionist.
        var denied = await fixture.Service.Get(id, fixture.RequesterId, [UserRoles.PATIENT]);
        var missing = await fixture.Service.Get(Guid.NewGuid().ToString(), fixture.RequesterId, [UserRoles.PATIENT]);

        Assert.False(denied.IsSuccess);
        Assert.Null(denied.Data);
        Assert.Equal(404, denied.Error!.HttpStatus);
        Assert.Equal(missing.Error!.Code, denied.Error.Code);
        Assert.Equal(missing.Error.Title, denied.Error.Title);
    }

    [Theory]
    [InlineData(UserRoles.PATIENT)]
    [InlineData(UserRoles.SUPERUSER)]
    public async Task ReceptionistProfileWithoutTheReceptionistTokenRoleDoesNotGrantHospitalAccess(string tokenRole)
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = await AddReceptionist(fixture, true);

        var result = await fixture.Service.Get(id, caller, [tokenRole]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task DoctorProfileWithoutTheDoctorTokenRoleDoesNotGrantAssignedDoctorAccess()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = (await fixture.Context.Doctors.SingleAsync()).UserId;

        var result = await fixture.Service.Get(id, caller, [UserRoles.PATIENT]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(404, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task MultipleRolesAllowAnExistingReceptionistScope()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = await AddReceptionist(fixture, true);

        var result = await fixture.Service.Get(id, caller, [UserRoles.PATIENT, UserRoles.RECEPTIONIST]);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Data!.BookingReference);
    }

    [Fact]
    public async Task CallerIdsAreComparedAsExactStringsAndEmptyRolesGrantNoAccess()
    {
        using var fixture = new BookingFixture();
        var id = await Book(fixture);
        var caller = (await fixture.Context.Patients.SingleAsync()).UserId;

        var wrongCase = await fixture.Service.Get(id, caller.ToUpperInvariant(), [UserRoles.PATIENT]);
        var noRoles = await fixture.Service.Get(id, caller, []);

        Assert.Equal(404, wrongCase.Error!.HttpStatus);
        Assert.Null(wrongCase.Data);
        Assert.Equal(404, noRoles.Error!.HttpStatus);
        Assert.Null(noRoles.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingIdentityIsRejectedBeforeStorageAccess(string? caller)
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        var service = new AppointmentService(context, null!);

        var result = await service.Get(Guid.NewGuid().ToString(), caller, [UserRoles.DOCTOR]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(401, result.Error!.HttpStatus);
    }

    [Fact]
    public async Task MalformedIdKeepsTheExistingBadRequestResponseBeforeStorageAccess()
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        var service = new AppointmentService(context, null!);

        var result = await service.Get("invalid", "signed-in-user", [UserRoles.PATIENT]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCode.SYS_InvalidGuid, result.Error!.Code);
        Assert.Equal(400, result.Error.HttpStatus);
    }

    internal static async Task<string> Book(BookingFixture fixture)
    {
        var result = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);
        Assert.True(result.IsSuccess);
        fixture.Context.ChangeTracker.Clear();
        return result.Data!.BookingReference;
    }

    internal static async Task<string> AddReceptionist(BookingFixture fixture, bool sameHospital)
    {
        var hospital = sameHospital ? await fixture.Context.Hospitals.SingleAsync() : new Hospital
        {
            Id = Guid.NewGuid(), Name = "Other hospital", Street = "Test street", City = "Test city",
            District = "Test district", Phone = "0000000000", Grade = "Test"
        };
        var user = new User { Id = "receptionist-user", FirstName = "Synthetic", LastName = "Receptionist" };
        fixture.Context.Receptionists.Add(new Receptionist
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user, HospitalId = hospital.Id, Hospital = hospital,
            Department = "Front office", Location = "Test desk"
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        return user.Id;
    }
}
