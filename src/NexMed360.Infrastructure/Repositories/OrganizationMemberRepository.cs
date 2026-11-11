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

    public Task<List<AccessRole>> FindRoles(Guid organizationId, IReadOnlyCollection<Guid> roleIds) => context.AccessRoles
        .Where(role => role.OrganizationId == organizationId && roleIds.Contains(role.Id)).ToListAsync();

    public async Task Save(OrganizationMember member, IReadOnlyCollection<Guid> roleIds, bool isNewMember)
    {
        if (isNewMember) context.OrganizationMembers.Add(member);

        if (roleIds.Count > 0)
        {
            var assignedRoleIds = await context.MemberRoleAssignments
                .Where(assignment => assignment.OrganizationMemberId == member.Id && assignment.OrganizationId == member.OrganizationId)
                .Select(assignment => assignment.AccessRoleId).ToListAsync();

            // Adding another assignment keeps the member's existing access roles.
            foreach (var roleId in roleIds.Distinct().Except(assignedRoleIds))
            {
                context.MemberRoleAssignments.Add(new MemberRoleAssignment
                {
                    OrganizationId = member.OrganizationId, OrganizationMemberId = member.Id, AccessRoleId = roleId
                });
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task SaveAdministrator(OrganizationMember member, AccessRole role, bool isNewMember, bool isNewRole)
    {
        if (isNewRole) context.AccessRoles.Add(role);
        await Save(member, [role.Id], isNewMember);
    }

    public Task<Location?> FindLocation(Guid organizationId, Guid locationId) => context.Locations
        .FirstOrDefaultAsync(row => row.OrganizationId == organizationId && row.Id == locationId);
}
