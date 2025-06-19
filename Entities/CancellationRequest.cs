using FinalProjectApi.Entities.Enums;

namespace FinalProjectApi.Entities;

public class CancellationRequest: Entity
{
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; }
    public Guid RequestedById { get; set; }
    public string Reason { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.CancelRequested;
}
