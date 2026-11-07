using System.Reflection;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360.Domain.Rules;

namespace NexMed360.API.Old.Tests.Compatibility;

public class DomainModelTests
{
    [Fact]
    public void ClinicalModelsHaveNoIdentityNavigationsOrStorageAnnotations()
    {
        var models = new[] { typeof(Appointment), typeof(CancellationRequest), typeof(ClinicalRecord), typeof(Prescription),
            typeof(Patient), typeof(PractitionerRole), typeof(Organization), typeof(OrganizationMembership), typeof(OrganizationMembership),
            typeof(PractitionerSchedule), typeof(SessionSlot), typeof(SessionHistory), typeof(VisitHistory), typeof(Entity) };
        foreach (var model in models)
        {
            Assert.Equal("NexMed360.Domain", model.Assembly.GetName().Name);
            Assert.DoesNotContain(model.GetProperties(), property => property.PropertyType == typeof(ApplicationUser));
            Assert.DoesNotContain(model.GetProperties().SelectMany(property => property.GetCustomAttributes()),
                attribute => attribute.GetType().Namespace == "System.ComponentModel.DataAnnotations.Schema");
        }
        Assert.Equal("NexMed360.Infrastructure", typeof(ApplicationUser).Assembly.GetName().Name);
        Assert.Equal(typeof(Guid), typeof(Patient).GetProperty("PersonId")!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(Practitioner).GetProperty("PersonId")!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(PractitionerRole).GetProperty("PractitionerId")!.PropertyType);
        Assert.Null(typeof(Patient).GetProperty("UserId"));
        Assert.Null(typeof(PractitionerRole).GetProperty("UserId"));
        Assert.Equal(typeof(string), typeof(Appointment).GetProperty("CreatedByUserId")!.PropertyType);
        Assert.Equal(typeof(string), typeof(CancellationRequest).GetProperty("RequestedById")!.PropertyType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CancellationRetainsTheCreatorAndNonCreatorOutcomes(bool creator)
    {
        var slot = new SessionSlot { IsAvailable = false };
        var appointment = new Appointment { CreatedByUserId = "opaque-owner" };
        var caller = creator ? "opaque-owner" : "opaque-staff";
        var request = CancellationRequest.Create(Guid.NewGuid(), caller, "Synthetic reason");
        appointment.Cancel(request, caller, slot);
        var expected = creator ? AppointmentStatus.Cancelled : AppointmentStatus.CancelRequested;
        Assert.Equal(expected, appointment.Status);
        Assert.Equal(expected, request.Status);
        Assert.Equal(creator, slot.IsAvailable);
        Assert.Equal(caller, request.RequestedById);
    }

    [Fact]
    public void BookingUsesTheSelectedSlotAndKeepsOpaqueUserIds()
    {
        var session = new PractitionerSchedule { PractitionerRoleId = Guid.NewGuid(), OrganizationId = Guid.NewGuid() };
        var slot = new SessionSlot { Id = Guid.NewGuid(), Session = session, Date = new DateOnly(2030, 2, 3), StartTime = new TimeOnly(9, 30), SlotNumber = 4 };
        var patient = Guid.NewGuid();
        var appointment = Appointment.Book(patient, slot, "opaque-account", "Synthetic note");
        Assert.Equal(patient, appointment.PatientId);
        Assert.Equal(session.PractitionerRoleId, appointment.PractitionerRoleId);
        Assert.Equal(session.OrganizationId, appointment.OrganizationId);
        Assert.Equal(slot.Id, appointment.SessionSlotId);
        Assert.Equal(new DateTime(2030, 2, 3, 9, 30, 0), appointment.AppointmentDateTime);
        Assert.Equal(4, appointment.Number);
        Assert.Equal("opaque-account", appointment.CreatedByUserId);
        Assert.Equal("Synthetic note", appointment.Remarks);
        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
    }

    [Fact]
    public void SessionHistoryKeepsEncounterCountingAndEndTimeBehavior()
    {
        var history = new SessionHistory();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        history.RecordEncounter(first); history.RecordEncounter(first); history.RecordEncounter(second);
        Assert.Equal(2, history.EncountersCount);
        Assert.Equal(second, history.LastKnownAppointmentId);
        history.ChangeStatus(SessionStatus.Ended, new DateTime(2030, 2, 3, 10, 20, 30));
        Assert.Equal(new TimeOnly(10, 20, 30), history.EndTime);
        Assert.Equal(SessionStatus.Ended, history.SessionStatus);
    }

    [Fact]
    public void ClinicalRecordUsesExistingServerVisitTimeAndPrescriptionValues()
    {
        var before = DateTime.UtcNow;
        var patient = Guid.NewGuid(); var practitionerRole = Guid.NewGuid();
        var prescription = new Prescription { MedicineName = "Synthetic medicine", Dosage = "Synthetic dosage", TimesPerDay = 2, IsLongTermUse = true };
        var record = ClinicalRecord.Create(patient, practitionerRole, "Synthetic diagnosis", null, null, [prescription]);
        Assert.Equal(patient, record.PatientId); Assert.Equal(practitionerRole, record.PractitionerRoleId);
        Assert.InRange(record.VisitDate, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, record.VisitDate.Kind);
        Assert.Same(prescription, Assert.Single(record.Prescriptions!));
        Assert.True(prescription.IsActive);
    }
}
