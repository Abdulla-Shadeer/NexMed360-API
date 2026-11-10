using NexMed360Api.Entities;

namespace NexMed360Api.Interfaces.Services;

public interface IOrganizationMemberRepository
{
    Task<OrganizationMember?> Find(Guid organizationId, string userId);
    Task<AccessRole?> FindRole(Guid organizationId, string name);
    Task Save(OrganizationMember member, AccessRole role, bool isNewMember, bool isNewRole);
    Task<Guid?> CreateLocation(Guid organizationId, string? name, string? department);
}
