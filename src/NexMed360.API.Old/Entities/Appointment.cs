using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Entities;

public class Appointment: Entity
{
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; }
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; }
    public Guid HospitalId { get; set; }
    public Hospital Hospital { get; set; }
    public string CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public int Number { get; set; }
    public Guid SessionSlotId { get; set; }
    public SessionSlot SessionSlot { get; set; }
    public string? Remarks { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
}
