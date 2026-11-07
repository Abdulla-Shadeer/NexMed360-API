using NexMed360Api.Entities;

namespace NexMed360Api.Interfaces.Services;

public interface IOrganizationRepository
{
    Task<bool> Exists(Guid id);
    Task<Organization?> Find(Guid id);
    Task<Organization> Add(Organization organization);
    Task<Organization> Save(Organization organization);
    Task<List<Organization>> GetAll();
    Task<bool> IsAdmin(Guid organizationId, string userId);
    Task<List<OrganizationMembership>> GetMembers(Guid organizationId);
}
