using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Common.Auth;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure.Repositories;

public class OrganizationMembershipRepository(ApplicationDbContext context) : IOrganizationMembershipRepository
{
    public async Task<OrganizationMembership> Add(OrganizationMembership membership, string roleName, IReadOnlyCollection<string> permissions)
    {
        var existing = await context.OrganizationMemberships.FirstOrDefaultAsync(row => row.OrganizationId == membership.OrganizationId && row.UserId == membership.UserId);
        context.Access.RequireOrganization(membership.OrganizationId);
        if (context.Access.IsSelected && context.Access.Mode != WorkspaceModes.Platform && !context.Access.Has(Permissions.MembersManage))
            throw new UnauthorizedAccessException("You need permission to manage staff.");
        if (existing == null) context.OrganizationMemberships.Add(membership);
        else membership = existing;
        var role = await context.AccessRoles.FirstOrDefaultAsync(row => row.OrganizationId == membership.OrganizationId && row.Name == roleName);
        if (role == null)
        {
            role = new AccessRole { OrganizationId = membership.OrganizationId, Name = roleName, Permissions = permissions.ToList() };
            context.AccessRoles.Add(role);
        }
        if (context.Access.Mode == WorkspaceModes.Platform) role.Permissions = permissions.Distinct().ToList();
        if (!await context.MembershipAccessRoles.AnyAsync(row => row.MembershipId == membership.Id && row.AccessRoleId == role.Id))
            context.MembershipAccessRoles.Add(new MembershipAccessRole { OrganizationId = membership.OrganizationId, MembershipId = membership.Id, AccessRoleId = role.Id });
        await context.SaveChangesAsync();
        return membership;
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
