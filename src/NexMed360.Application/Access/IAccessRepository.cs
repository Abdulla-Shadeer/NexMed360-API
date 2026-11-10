using NexMed360Api.Entities;

namespace NexMed360.Application.Access;

public interface IAccessRepository
{
    Task<List<AccessRole>> MemberRoles(Guid organizationId, Guid organizationMemberId);
    Task<bool> RoleIsShared(Guid roleId, Guid organizationMemberId);
    Task<string> MemberName(string userId);
    Task SaveAdministrator(OrganizationMember member, AccessRole role, bool isNewRole);
    Task RemoveMember(OrganizationMember member);
    Task<Organization?> FindOrganization(Guid organizationId);
    Task<bool> HasPatientHistory(Guid organizationId);
    Task<bool> DeleteOrganization(Organization organization);
    Task<List<AccessRole>> Roles(Guid? organizationId = null);
    Task<AccessRole?> FindRole(Guid id);
    Task<bool> RoleNameExists(Guid organizationId, string name, Guid? exceptId = null);
    Task SaveRole(AccessRole role, bool isNew);
    Task<List<OrganizationMemberDto>> Members(Guid? organizationId = null);
    Task<OrganizationMember?> FindMember(Guid id);
    Task<List<LocationDto>> Locations();
    Task SaveMember(OrganizationMember member, IReadOnlyCollection<Guid> roleIds);
}
