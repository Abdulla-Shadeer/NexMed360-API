namespace NexMed360Api.Entities;

public class OrganizationMembership : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = "";
    public Guid? LocationId { get; set; }
    public bool IsActive { get; set; } = true;
}
