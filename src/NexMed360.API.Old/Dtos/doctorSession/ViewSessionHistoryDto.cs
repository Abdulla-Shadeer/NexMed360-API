using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Dtos.doctorSession;

public class ViewSessionHistoryDto
{
    public string Id { get; set; }
    public string DoctorSessionId { get; set; }
    public string DoctorName { get; set; }
    public string DoctorSpeciality { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public TimeOnly StartedTime { get; set; }
    public TimeOnly EndedTime { get; set; }
    public SessionStatus SessionStatus { get; set; }
    public int EncountersCount { get; set; }
    public string? LastKnownAppointmentId { get; set; }
}
