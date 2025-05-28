using FinalProjectApi.Entities.Enums;

namespace FinalProjectApi.Entities;

public class Appointment: Entity
{
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; }
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Remarks { get; set; }
    public string Location { get; set; }
    public AppointmentStatus Status { get; set; }
}
