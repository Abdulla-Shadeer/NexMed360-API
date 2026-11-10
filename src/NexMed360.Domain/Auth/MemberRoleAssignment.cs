namespace NexMed360Api.Entities;

public class MemberRoleAssignment : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public Guid OrganizationMemberId { get; set; }
    public Guid AccessRoleId { get; set; }
}
