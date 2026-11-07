using NexMed360.API.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.practitionerSchedule;

public class CreatePractitionerScheduleDto
{
    [Required]
    public string PractitionerRoleId { get; set; }
    [Required]
    public TimeOnly StartTime { get; set; }
    [Required]
    public TimeOnly EndTime { get; set; }
    [Required]
    public string OrganizationId { get; set; }
    public int? SlotDurationMinutes { get; set; }
    [Required]
    public List<SessionOccurrence> Occurrences { get; set; }
}
