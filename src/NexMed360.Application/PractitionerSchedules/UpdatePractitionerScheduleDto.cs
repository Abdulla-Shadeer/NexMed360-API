using NexMed360.API.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace NexMed360.API.Dtos.practitionerSchedule;

public class UpdatePractitionerScheduleDto
{
    [Required]
    public required TimeOnly StartTime { get; set; }
    [Required]
    public required TimeOnly EndTime { get; set; }
    [Required]
    public required int SlotDurationMinutes { get; set; }
    [Required]
    public required List<SessionOccurrence> Occurrences { get; set; }
    [Required]
    public required bool IsActive { get; set; }
}
