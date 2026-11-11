using NexMed360Api.Entities;

namespace NexMed360Api.Interfaces.Services;

public interface IOrganizationMemberRepository
{
    Task<OrganizationMember?> Find(Guid organizationId, string userId);
    Task<AccessRole?> FindRole(Guid organizationId, string name);
    Task<List<AccessRole>> FindRoles(Guid organizationId, IReadOnlyCollection<Guid> roleIds);
    Task Save(OrganizationMember member, IReadOnlyCollection<Guid> roleIds, bool isNewMember);
    Task SaveAdministrator(OrganizationMember member, AccessRole role, bool isNewMember, bool isNewRole);
    Task<Location?> FindLocation(Guid organizationId, Guid locationId);
}
