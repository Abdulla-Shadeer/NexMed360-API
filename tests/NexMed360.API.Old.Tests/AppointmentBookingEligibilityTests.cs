using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexMed360.API.Entities.Enums;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Services;

namespace NexMed360.API.Old.Tests;

public class AppointmentBookingEligibilityTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public async Task IneligibleSlotOrSessionIsRejectedWithoutMutation(
        bool sessionIsActive, bool slotIsAvailable, bool bookingAllowed)
    {
        using var fixture = new Fixture(sessionIsActive, slotIsAvailable, bookingAllowed);

        var result = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCode.APPT_DoctorUnavailable, result.Error!.Code);
        Assert.Equal(409, result.Error.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Empty(await fixture.Context.Appointments.ToListAsync());
        var slot = await fixture.Context.SessionSlots.SingleAsync();
        var session = await fixture.Context.DoctorSessions.SingleAsync();
        Assert.Equal(slotIsAvailable, slot.IsAvailable);
        Assert.Equal(bookingAllowed, slot.BookingAllowed);
        Assert.Equal(sessionIsActive, session.IsActive);
    }

    [Fact]
    public async Task EligibleSlotCreatesAppointmentAndBecomesUnavailable()
    {
        using var fixture = new Fixture();

        var result = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(fixture.PatientId.ToString(), result.Data.PatientId);
        Assert.Equal(fixture.RequesterId, result.Data.CreatedByUserId);
        Assert.Equal("Synthetic Patient", result.Data.PatientName);
        Assert.Equal("Synthetic Doctor", result.Data.DoctorName);
        Assert.Equal("Synthetic hospital", result.Data.HospitalName);
        Assert.Equal(AppointmentStatus.Pending, result.Data.Status);
        Assert.Equal("Synthetic booking", result.Data.Remarks);
        fixture.Context.ChangeTracker.Clear();
        var appointment = Assert.Single(await fixture.Context.Appointments.ToListAsync());
        Assert.Equal(appointment.Id.ToString(), result.Data.BookingReference);
        Assert.Equal(fixture.SlotId, appointment.SessionSlotId);
        Assert.Equal(fixture.PatientId, appointment.PatientId);
        Assert.Equal(fixture.RequesterId, appointment.CreatedByUserId);
        Assert.False((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Fact]
    public async Task SequentialRepeatBookingIsRejectedAndKeepsOriginalAppointment()
    {
        using var fixture = new Fixture();
        var first = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);
        Assert.True(first.IsSuccess);
        fixture.Context.ChangeTracker.Clear();

        var second = await fixture.Service.Create(fixture.Request(), fixture.RequesterId);

        Assert.False(second.IsSuccess);
        Assert.Null(second.Data);
        Assert.Equal(409, second.Error!.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        var appointment = Assert.Single(await fixture.Context.Appointments.ToListAsync());
        Assert.Equal(first.Data!.BookingReference, appointment.Id.ToString());
        Assert.False((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    [Fact]
    public async Task MissingSlotKeepsExistingBadRequestResponseWithoutMutation()
    {
        using var fixture = new Fixture();
        var request = fixture.Request();
        request.SessionSlotId = Guid.NewGuid();

        var result = await fixture.Service.Create(request, fixture.RequesterId);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCode.SYS_BadRequest, result.Error!.Code);
        Assert.Equal(400, result.Error.HttpStatus);
        fixture.Context.ChangeTracker.Clear();
        Assert.Empty(await fixture.Context.Appointments.ToListAsync());
        Assert.True((await fixture.Context.SessionSlots.SingleAsync()).IsAvailable);
    }

    internal sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Context { get; }
        public AppointmentService Service { get; }
        public Guid PatientId { get; } = Guid.NewGuid();
        public Guid SlotId { get; } = Guid.NewGuid();
        private readonly string _userSuffix = Guid.NewGuid().ToString("N");
        public string RequesterId => "booking-" + _userSuffix;

        public Fixture(bool sessionIsActive = true, bool slotIsAvailable = true, bool bookingAllowed = true)
            : this(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
                sessionIsActive, slotIsAvailable, bookingAllowed)
        {
        }

        public Fixture(DbContextOptions<ApplicationDbContext> options,
            bool sessionIsActive = true, bool slotIsAvailable = true, bool bookingAllowed = true)
        {
            Context = new ApplicationDbContext(options);
            var patientUser = new User
            {
                Id = "patient-" + _userSuffix, FirstName = "Synthetic", LastName = "Patient",
                Email = "patient@example.test", PhoneNumber = "1111111111"
            };
            var patient = new Patient
            {
                Id = PatientId, UserId = patientUser.Id, User = patientUser,
                Gender = "Test", BirthDate = new DateOnly(2000, 1, 1)
            };
            var hospital = new Hospital
            {
                Id = Guid.NewGuid(), Name = "Synthetic hospital", Street = "Test street",
                City = "Test city", District = "Test district", Phone = "0000000000", Grade = "Test"
            };
            var doctorUser = new User { Id = "doctor-" + _userSuffix, FirstName = "Synthetic", LastName = "Doctor" };
            var doctor = new Doctor
            {
                Id = Guid.NewGuid(), UserId = doctorUser.Id, User = doctorUser,
                HospitalId = hospital.Id, Hospital = hospital, Speciality = "Test speciality"
            };
            var session = new DoctorSession
            {
                Id = Guid.NewGuid(), DoctorId = doctor.Id, Doctor = doctor,
                HospitalId = hospital.Id, Hospital = hospital,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0),
                Occurrences = [SessionOccurrence.AllDays], IsActive = sessionIsActive
            };
            var slot = new SessionSlot
            {
                Id = SlotId, SessionId = session.Id, Session = session,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 30), SlotNumber = 1,
                IsAvailable = slotIsAvailable, BookingAllowed = bookingAllowed
            };
            Context.Users.Add(new User { Id = RequesterId, FirstName = "Synthetic", LastName = "Booker" });
            Context.Patients.Add(patient);
            Context.SessionSlots.Add(slot);
            Context.SaveChanges();
            Context.ChangeTracker.Clear();
            var mapper = new MapperConfiguration(
                cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
            Service = new AppointmentService(Context, mapper);
        }

        public CreateAppointmentDto Request() => new()
        {
            PatientId = PatientId, SessionSlotId = SlotId, Remarks = "Synthetic booking"
        };

        public void Dispose() => Context.Dispose();
    }
}
