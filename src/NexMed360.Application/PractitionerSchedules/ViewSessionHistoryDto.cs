using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Dtos.practitionerSchedule;

public class ViewSessionHistoryDto
{
    public string Id { get; set; }
    public string PractitionerScheduleId { get; set; }
    public string PractitionerName { get; set; }
    public string PractitionerSpecialty { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public TimeOnly StartedTime { get; set; }
    public TimeOnly EndedTime { get; set; }
    public SessionStatus SessionStatus { get; set; }
    public int EncountersCount { get; set; }
    public string? LastKnownAppointmentId { get; set; }
}
