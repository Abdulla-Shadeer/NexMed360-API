using NexMed360Api.Entities;

namespace NexMed360.Application.Access;

public interface IAccessRepository
{
    Task<List<AccessRole>> MemberRoles(Guid organizationId, Guid membershipId);
    Task<bool> RoleIsShared(Guid roleId, Guid membershipId);
    Task<string> MemberName(string userId);
    Task SaveAdministrator(OrganizationMembership member, AccessRole role, bool isNewRole);
    Task RemoveMember(OrganizationMembership member);
    Task<Organization?> FindOrganization(Guid organizationId);
    Task<bool> HasPatientHistory(Guid organizationId);
    Task<bool> DeleteOrganization(Organization organization);
    Task<List<AccessRole>> Roles(Guid? organizationId = null);
    Task<AccessRole?> FindRole(Guid id);
    Task<bool> RoleNameExists(Guid organizationId, string name, Guid? exceptId = null);
    Task SaveRole(AccessRole role, bool isNew);
    Task<List<MembershipDto>> Members(Guid? organizationId = null);
    Task<OrganizationMembership?> FindMember(Guid id);
    Task<List<LocationDto>> Locations();
    Task SaveMember(OrganizationMembership membership, IReadOnlyCollection<Guid> roleIds);
}
