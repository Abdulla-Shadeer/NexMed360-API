using NexMed360.API.Entities.Enums;

namespace NexMed360Api.Dtos.practitionerSchedule;

public class ViewPractitionerScheduleDto
{
    public Guid Id { get; set; }
    public Guid PractitionerRoleId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string OrganizationAddress { get; set; }
    public int SlotDurationMinutes { get; set; }
    public int TotalSlots { get; set; }
    public List<SessionOccurrence> Occurrences { get; set; }
    public bool IsActive { get; set; }
}
