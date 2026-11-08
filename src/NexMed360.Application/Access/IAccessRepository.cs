using NexMed360Api.Entities;

namespace NexMed360.Application.Access;

public interface IAccessRepository
{
    Task<OrganizationAdministratorsDto?> OrganizationAdministrators(Guid organizationId);
    Task<bool> UpdateAdministrator(Guid organizationId, Guid membershipId, UpdateOrganizationAdministratorDto dto);
    Task<bool> RemoveAdministrator(Guid organizationId, Guid membershipId);
    Task<OrganizationDeletionOutcome> DeleteOrganization(Guid organizationId);
    Task<List<AccessRole>> Roles();
    Task<AccessRole?> FindRole(Guid id);
    Task<bool> RoleNameExists(string name, Guid? exceptId);
    Task SaveRole(AccessRole role, bool isNew);
    Task<List<MembershipDto>> Members();
    Task<OrganizationMembership?> FindMember(Guid id);
    Task<List<LocationDto>> Locations();
    Task SaveMember(OrganizationMembership membership, IReadOnlyCollection<Guid> roleIds);
}
