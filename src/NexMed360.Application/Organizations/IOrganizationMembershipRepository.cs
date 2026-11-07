using NexMed360Api.Entities;
namespace NexMed360Api.Interfaces.Services;

public interface IOrganizationMembershipRepository
{
    Task<OrganizationMembership> Add(OrganizationMembership membership, string roleName, IReadOnlyCollection<string> permissions);
    Task<Guid?> CreateLocation(Guid organizationId, string? name, string? department);
}
