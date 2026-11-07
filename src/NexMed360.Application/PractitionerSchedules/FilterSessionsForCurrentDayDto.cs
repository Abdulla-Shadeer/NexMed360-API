using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.practitionerSchedule;

public class FilterSessionsForCurrentDayDto
{
    [Required]
    public string Date { get; set; }
    [Required]
    public string PractitionerRoleId { get; set; }
    [Required]
    public string OrganizationId { get; set; }
}
