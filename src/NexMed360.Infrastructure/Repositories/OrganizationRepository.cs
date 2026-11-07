using Microsoft.EntityFrameworkCore;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class OrganizationRepository(ApplicationDbContext context) : IOrganizationRepository
{
    public Task<bool> Exists(Guid id) => context.Organizations.AnyAsync(row => row.Id == id);
    public Task<Organization?> Find(Guid id) => context.Organizations.FirstOrDefaultAsync(row => row.Id == id);
    public async Task<Organization> Add(Organization row) { context.Organizations.Add(row); await context.SaveChangesAsync(); return row; }
    public async Task<Organization> Save(Organization row) { await context.SaveChangesAsync(); return row; }
    public Task<List<Organization>> GetAll() => context.Organizations.ToListAsync();
    public Task<bool> IsAdmin(Guid id, string userId) => context.OrganizationMemberships.AnyAsync(member => member.OrganizationId == id && member.UserId == userId && member.IsActive
        && context.MembershipAccessRoles.Any(link => link.MembershipId == member.Id && context.AccessRoles.Any(role => role.Id == link.AccessRoleId && role.Permissions.Contains(Permissions.MembersManage))));
    public Task<List<OrganizationMembership>> GetMembers(Guid id) => context.OrganizationMemberships.Where(row => row.OrganizationId == id).ToListAsync();
}
