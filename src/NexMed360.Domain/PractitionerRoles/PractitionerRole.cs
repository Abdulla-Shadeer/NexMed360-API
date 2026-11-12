namespace NexMed360Api.Entities;

public class PractitionerRole : Entity, IOrganizationOwned
{
    public Guid PractitionerId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? LocationId { get; set; }
    public string RoleName { get; set; } = "Practitioner";
    public string Specialty { get; set; } = "";
    public List<string> Services { get; set; } = [];
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsSameAssignmentAs(PractitionerRole other) =>
        PractitionerId == other.PractitionerId && OrganizationId == other.OrganizationId && LocationId == other.LocationId
        && string.Equals(RoleName.Trim(), other.RoleName.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(Specialty.Trim(), other.Specialty.Trim(), StringComparison.OrdinalIgnoreCase)
        && StartDate == other.StartDate && EndDate == other.EndDate && IsActive == other.IsActive;
}
