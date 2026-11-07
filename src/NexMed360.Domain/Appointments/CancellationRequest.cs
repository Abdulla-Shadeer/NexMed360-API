using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Entities;

public class CancellationRequest: Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; }
    public string RequestedById { get; set; } = null!;
    public string Reason { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.CancelRequested;

    public static CancellationRequest Create(Guid appointmentId, string callerId, string reason) => new()
    {
        AppointmentId = appointmentId, RequestedById = callerId, Reason = reason, CreatedDate = DateTime.UtcNow
    };
}
