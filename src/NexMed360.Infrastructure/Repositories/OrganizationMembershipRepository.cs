using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure.Repositories;

public class OrganizationMembershipRepository(ApplicationDbContext context) : IOrganizationMembershipRepository
{
    public Task<OrganizationMembership?> Find(Guid organizationId, string userId) =>
        context.OrganizationMemberships.FirstOrDefaultAsync(row => row.OrganizationId == organizationId && row.UserId == userId);

    public Task<AccessRole?> FindRole(Guid organizationId, string name) =>
        context.AccessRoles.FirstOrDefaultAsync(row => row.OrganizationId == organizationId && row.Name == name);

    public async Task Save(OrganizationMembership member, AccessRole role, bool isNewMember, bool isNewRole)
    {
        if (isNewMember) context.OrganizationMemberships.Add(member);
        if (isNewRole) context.AccessRoles.Add(role);

        if (!await context.MembershipAccessRoles.AnyAsync(row => row.MembershipId == member.Id && row.AccessRoleId == role.Id))
        {
            context.MembershipAccessRoles.Add(new MembershipAccessRole
            {
                OrganizationId = member.OrganizationId, MembershipId = member.Id, AccessRoleId = role.Id
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task<Guid?> CreateLocation(Guid organizationId, string? name, string? department)
    {
        Location? parent = null;
        if (!string.IsNullOrWhiteSpace(department))
        {
            parent = await context.Locations.FirstOrDefaultAsync(row => row.OrganizationId == organizationId && row.Name == department && row.Type == LocationType.Department);
            if (parent == null)
            {
                parent = new Location { OrganizationId = organizationId, Name = department, Type = LocationType.Department };
                context.Locations.Add(parent);
            }
        }
        if (string.IsNullOrWhiteSpace(name)) { await context.SaveChangesAsync(); return parent?.Id; }
        var location = await context.Locations.FirstOrDefaultAsync(row => row.OrganizationId == organizationId && row.Name == name && row.ParentId == (parent == null ? null : parent.Id));
        if (location == null)
        {
            location = new Location { OrganizationId = organizationId, Name = name, ParentId = parent?.Id, Type = LocationType.Other };
            context.Locations.Add(location);
        }
        await context.SaveChangesAsync();
        return location.Id;
    }
}
