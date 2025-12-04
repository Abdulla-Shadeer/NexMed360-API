using NexMed360.API.Entities.Enums;

namespace NexMed360Api.Dtos.doctorSession;

public class ViewDoctorSessionDto
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid HospitalId { get; set; }
    public string HospitalName { get; set; }
    public string HospitalAddress { get; set; }
    public int SlotDurationMinutes { get; set; }
    public int TotalSlots { get; set; }
    public List<SessionOccurrence> Occurrences { get; set; }
    public bool IsActive { get; set; }
}
