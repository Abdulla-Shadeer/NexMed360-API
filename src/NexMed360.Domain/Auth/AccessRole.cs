namespace NexMed360Api.Entities;

public class AccessRole : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public List<string> Permissions { get; set; } = [];
}
