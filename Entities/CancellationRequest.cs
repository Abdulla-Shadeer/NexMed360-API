using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Entities;

public class CancellationRequest: Entity
{
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; }
    public Guid RequestedById { get; set; }
    public string Reason { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.CancelRequested;
}
