namespace NexMed360Api.Entities;

public class AccessRole : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public List<string> Permissions { get; set; } = [];
}

public class MembershipAccessRole : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid AccessRoleId { get; set; }
}
