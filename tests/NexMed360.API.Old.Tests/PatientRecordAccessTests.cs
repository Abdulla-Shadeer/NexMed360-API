using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Services;

namespace NexMed360.API.Old.Tests;

public class PatientRecordAccessTests
{
    [Fact]
    public async Task PatientCanReadOwnRecordAndPrescription()
    {
        using var fixture = new Fixture();

        var result = await fixture.Service.ViewClinicRecord(
            fixture.OwnRecord.Id.ToString(), fixture.Owner.UserId, false);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(fixture.OwnRecord.Id.ToString(), result.Data.Id);
        Assert.Equal("Owner Patient", result.Data.PatientName);
        Assert.Equal("Doctor User", result.Data.DoctorName);
        var prescription = Assert.Single(result.Data.Prescriptions!);
        Assert.Equal("Synthetic medicine", prescription.MedicineName);
        Assert.Equal("Synthetic hospital", prescription.IssuedAtHospital);
    }

    [Fact]
    public async Task PatientCanReadOnlyTheirRecordList()
    {
        using var fixture = new Fixture();

        var result = await fixture.Service.ViewAllClinicRecords(
            fixture.Owner.Id.ToString(), fixture.Owner.UserId, false);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, record => record.Id == fixture.OwnRecord.Id.ToString());
        Assert.DoesNotContain(result.Data, record => record.Id == fixture.OtherRecord.Id.ToString());
    }

    [Fact]
    public async Task PatientCannotReadAnotherPatientsRecordOrPrescription()
    {
        using var fixture = new Fixture();

        var denied = await fixture.Service.ViewClinicRecord(
            fixture.OtherRecord.Id.ToString(), fixture.Owner.UserId, false);
        var missing = await fixture.Service.ViewClinicRecord(
            Guid.NewGuid().ToString(), fixture.Owner.UserId, false);

        Assert.False(denied.IsSuccess);
        Assert.Null(denied.Data);
        Assert.Equal(404, denied.Error!.HttpStatus);
        Assert.Equal(missing.Error!.Code, denied.Error.Code);
        Assert.Equal(missing.Error.Title, denied.Error.Title);
    }

    [Fact]
    public async Task PatientCannotReadAnotherPatientsRecordList()
    {
        using var fixture = new Fixture();

        var denied = await fixture.Service.ViewAllClinicRecords(
            fixture.Other.Id.ToString(), fixture.Owner.UserId, false);
        var missing = await fixture.Service.ViewAllClinicRecords(
            Guid.NewGuid().ToString(), fixture.Owner.UserId, false);

        Assert.False(denied.IsSuccess);
        Assert.Null(denied.Data);
        Assert.Equal(404, denied.Error!.HttpStatus);
        Assert.Equal(missing.Error!.Code, denied.Error.Code);
        Assert.Equal(missing.Error.Title, denied.Error.Title);
    }

    [Fact]
    public async Task EmptyRecordListStillRequiresPatientOwnership()
    {
        using var fixture = new Fixture();

        var own = await fixture.Service.ViewAllClinicRecords(
            fixture.EmptyPatient.Id.ToString(), fixture.EmptyPatient.UserId, false);
        var other = await fixture.Service.ViewAllClinicRecords(
            fixture.EmptyPatient.Id.ToString(), fixture.Owner.UserId, false);

        Assert.True(own.IsSuccess);
        Assert.NotNull(own.Data);
        Assert.Empty(own.Data);
        Assert.False(other.IsSuccess);
        Assert.Null(other.Data);
        Assert.Equal(404, other.Error!.HttpStatus);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    public async Task MissingCallerIdentityCannotReadRecords(string? requesterUserId, bool isDoctor)
    {
        using var fixture = new Fixture();

        var single = await fixture.Service.ViewClinicRecord(
            fixture.OwnRecord.Id.ToString(), requesterUserId, isDoctor);
        var list = await fixture.Service.ViewAllClinicRecords(
            fixture.Owner.Id.ToString(), requesterUserId, isDoctor);

        Assert.False(single.IsSuccess);
        Assert.Null(single.Data);
        Assert.Equal(401, single.Error!.HttpStatus);
        Assert.Equal(ErrorCode.SYS_Unauthorized, single.Error.Code);
        Assert.False(list.IsSuccess);
        Assert.Null(list.Data);
        Assert.Equal(401, list.Error!.HttpStatus);
        Assert.Equal(ErrorCode.SYS_Unauthorized, list.Error.Code);
    }

    [Theory]
    [InlineData("no-patient-profile")]
    [InlineData("OWNER-USER")]
    public async Task CallerWithoutMatchingPatientOwnershipCannotReadRecords(string requesterUserId)
    {
        using var fixture = new Fixture();

        var single = await fixture.Service.ViewClinicRecord(
            fixture.OwnRecord.Id.ToString(), requesterUserId, false);
        var list = await fixture.Service.ViewAllClinicRecords(
            fixture.Owner.Id.ToString(), requesterUserId, false);

        Assert.False(single.IsSuccess);
        Assert.Null(single.Data);
        Assert.Equal(404, single.Error!.HttpStatus);
        Assert.False(list.IsSuccess);
        Assert.Null(list.Data);
        Assert.Equal(404, list.Error!.HttpStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidAndMissingIdsKeepExistingFailureResponses(bool isDoctor)
    {
        using var fixture = new Fixture();
        var requester = isDoctor ? fixture.Doctor.UserId : fixture.Owner.UserId;

        var invalidRecord = await fixture.Service.ViewClinicRecord("invalid", requester, isDoctor);
        var invalidPatient = await fixture.Service.ViewAllClinicRecords("invalid", requester, isDoctor);
        var missingRecord = await fixture.Service.ViewClinicRecord(Guid.NewGuid().ToString(), requester, isDoctor);
        var missingPatient = await fixture.Service.ViewAllClinicRecords(Guid.NewGuid().ToString(), requester, isDoctor);

        Assert.Equal(400, invalidRecord.Error!.HttpStatus);
        Assert.Equal(ErrorCode.SYS_InvalidGuid, invalidRecord.Error.Code);
        Assert.Equal(400, invalidPatient.Error!.HttpStatus);
        Assert.Equal(ErrorCode.SYS_InvalidGuid, invalidPatient.Error.Code);
        Assert.Equal(404, missingRecord.Error!.HttpStatus);
        Assert.Equal(ErrorCode.CLNRD_NotFound, missingRecord.Error.Code);
        Assert.Equal(404, missingPatient.Error!.HttpStatus);
        Assert.Equal(ErrorCode.USR_NotFound, missingPatient.Error.Code);
    }

    [Fact]
    public async Task DoctorCanStillReadPatientRecords()
    {
        using var fixture = new Fixture();

        var single = await fixture.Service.ViewClinicRecord(
            fixture.OtherRecord.Id.ToString(), fixture.Doctor.UserId, true);
        var list = await fixture.Service.ViewAllClinicRecords(
            fixture.Owner.Id.ToString(), fixture.Doctor.UserId, true);

        Assert.True(single.IsSuccess);
        Assert.Equal(fixture.OtherRecord.Id.ToString(), single.Data!.Id);
        Assert.True(list.IsSuccess);
        Assert.Equal(2, list.Data!.Count);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ApplicationDbContext _context;
        public PatientService Service { get; }
        public Patient Owner { get; }
        public Patient Other { get; }
        public Patient EmptyPatient { get; }
        public Doctor Doctor { get; }
        public ClinicRecord OwnRecord { get; }
        public ClinicRecord OtherRecord { get; }

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            _context = new ApplicationDbContext(options);

            Owner = Patient("owner-user", "Owner");
            Other = Patient("other-user", "Other");
            EmptyPatient = Patient("empty-user", "Empty");
            var hospital = new Hospital
            {
                Id = Guid.NewGuid(), Name = "Synthetic hospital", Street = "Test street",
                City = "Test city", District = "Test district", Phone = "0000000000", Grade = "Test"
            };
            Doctor = new Doctor
            {
                Id = Guid.NewGuid(), UserId = "doctor-user", Speciality = "Test speciality",

                HospitalId = hospital.Id,
            };
            OwnRecord = Record(Owner);
            OtherRecord = Record(Other);
            OwnRecord.Prescriptions!.Add(new Prescription
            {
                Id = Guid.NewGuid(), ClinicRecordId = OwnRecord.Id, ClinicRecord = OwnRecord,
                MedicineName = "Synthetic medicine", Dosage = "Synthetic dosage", TimesPerDay = 1
            });
            _context.Users.Add(new User { Id = "doctor-user", FirstName = "Doctor", LastName = "User" });
            _context.Hospitals.Add(hospital);
            _context.Doctors.Add(Doctor);
            _context.Patients.AddRange(Owner, Other, EmptyPatient);
            _context.ClinicRecords.AddRange(OwnRecord, Record(Owner), OtherRecord);
            _context.SaveChanges();
            // Force the read methods to load their own related data.
            _context.ChangeTracker.Clear();

            // These read methods never use the account-registration dependency.
            Service = new PatientService(null!, new PatientRepository(_context), null!, TestViews.For(_context), new ClinicRecordRepository(_context));
        }

        private Patient Patient(string userId, string firstName)
        {
            var patient = new Patient { Id = Guid.NewGuid(), UserId = userId, Gender = "Test", BirthDate = new DateOnly(2000, 1, 1) };
            _context.Users.Add(new User { Id = userId, FirstName = firstName, LastName = "Patient", });
            return patient;
        }

        private ClinicRecord Record(Patient patient) => new()
        {
            Id = Guid.NewGuid(), PatientId = patient.Id,
            DoctorId = Doctor.Id,  VisitDate = DateTime.UtcNow,
            Diagnosis = "Synthetic diagnosis", DoctorNotes = "Synthetic note"
        };

        public void Dispose() => _context.Dispose();
    }
}
