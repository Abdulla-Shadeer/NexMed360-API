using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Dtos.practitionerSchedule;

public class FilterPractitionerScheduleDto
{
    public DateOnly? Date { get; set; }
    public string? OrganizationId { get; set; }
    public string? PractitionerRoleId { get; set; }
    public SessionStatus? SessionStatus { get; set; }
}
