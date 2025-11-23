namespace NexMed360Api.Dtos.doctorSession;

public class ViewCurrentOrNextSessionDto
{
    public bool IsCurrent { get; set; }
    public string SessionId { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }

    // below are applicable only if the session is current
    public string? StartedTime { get; set; }
    public string? LastKnownAppointmentId { get; set; }
    public int? EncountersCount { get; set; }
}
