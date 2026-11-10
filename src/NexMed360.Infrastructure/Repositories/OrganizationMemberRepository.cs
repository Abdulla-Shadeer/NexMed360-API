using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure.Repositories;

public class OrganizationMemberRepository(ApplicationDbContext context) : IOrganizationMemberRepository
{
    // The service must detect existing access outside its location scope before making changes.
    public Task<OrganizationMember?> Find(Guid organizationId, string userId) =>
        context.OrganizationMembers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.OrganizationId == organizationId && row.UserId == userId);

    public Task<AccessRole?> FindRole(Guid organizationId, string name) =>
        context.AccessRoles.FirstOrDefaultAsync(row => row.OrganizationId == organizationId && row.Name == name);

    public async Task Save(OrganizationMember member, AccessRole role, bool isNewMember, bool isNewRole)
    {
        if (isNewMember) context.OrganizationMembers.Add(member);
        if (isNewRole) context.AccessRoles.Add(role);

        if (!await context.MemberRoleAssignments.AnyAsync(row => row.OrganizationMemberId == member.Id && row.AccessRoleId == role.Id))
        {
            context.MemberRoleAssignments.Add(new MemberRoleAssignment
            {
                OrganizationId = member.OrganizationId, OrganizationMemberId = member.Id, AccessRoleId = role.Id
            });
        }

        await context.SaveChangesAsync();
    }

    public Task<Location?> FindLocation(Guid organizationId, Guid locationId) => context.Locations
        .FirstOrDefaultAsync(row => row.OrganizationId == organizationId && row.Id == locationId);
}
