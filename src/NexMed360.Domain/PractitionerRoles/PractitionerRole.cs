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
}
