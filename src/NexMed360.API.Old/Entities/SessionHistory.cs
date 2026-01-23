using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Entities;

public class SessionHistory: Entity
{
    public Guid DoctorSessionId { get; set; }
    public DoctorSession DoctorSession { get; set; }
    public DateOnly Date { get; set; } = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day);
    public TimeOnly StartTime { get; set; } = new TimeOnly(DateTime.Now.TimeOfDay.Ticks);
    public TimeOnly? EndTime { get; set; }
    public SessionStatus SessionStatus { get; set; } = SessionStatus.Started;
    public Guid? LastKnownAppointmentId { get; set; }
    public int EncountersCount { get; set; } = 0;
}
