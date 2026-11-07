using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Entities;

public class Appointment: Entity
{
    public Guid PatientId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid HospitalId { get; set; }

    public string CreatedByUserId { get; set; }

    public DateTime AppointmentDateTime { get; set; }
    public int Number { get; set; }
    public Guid SessionSlotId { get; set; }

    public string? Remarks { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    public bool IsCancelled => Status == AppointmentStatus.Cancelled;
    public bool HasCancellationRequest => Status == AppointmentStatus.CancelRequested;
    public bool IsCreator(string callerId) => CreatedByUserId == callerId;

    public static Appointment Book(Guid patientId, SessionSlot slot, string callerId, string? remarks) => new()
    {
        PatientId = patientId, DoctorId = slot.Session.DoctorId, HospitalId = slot.Session.HospitalId,
        CreatedByUserId = callerId, SessionSlotId = slot.Id,
        AppointmentDateTime = slot.Date.ToDateTime(slot.StartTime), Number = slot.SlotNumber, Remarks = remarks
    };

    public void Cancel(CancellationRequest request, string callerId, SessionSlot slot)
    {
        Status = IsCreator(callerId) ? AppointmentStatus.Cancelled : AppointmentStatus.CancelRequested;
        request.Status = Status;
        if (IsCreator(callerId)) slot.IsAvailable = true;
    }

    public void ChangeStatus(AppointmentStatus status) => Status = status;
}
