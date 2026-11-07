using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Dtos.practitionerSchedule;

public class FilterSessionHistoryDto
{
    public string? SessionId { get; set; }
    public string? PractitionerRoleId { get; set; }
    public string? OrganizationId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public SessionStatus? Status { get; set; }
}
