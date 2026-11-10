namespace NexMed360Api.Entities;

public class OrganizationMember : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = "";
    public Guid? LocationId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<MemberRoleAssignment> RoleAssignments { get; set; } = [];
}
