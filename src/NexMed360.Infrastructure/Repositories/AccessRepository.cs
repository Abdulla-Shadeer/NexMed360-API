using NexMed360Api.Common.Auth;
using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Access;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Repositories;

public sealed class AccessRepository(ApplicationDbContext context) : IAccessRepository
{
    public Task<List<AccessRole>> Roles() => context.AccessRoles.OrderBy(row => row.Name).ToListAsync();

    public Task<AccessRole?> FindRole(Guid id) => context.AccessRoles.FirstOrDefaultAsync(row => row.Id == id);

    public Task<bool> RoleNameExists(string name, Guid? exceptId) => context.AccessRoles.AnyAsync(row => row.Name == name && row.Id != exceptId);

    public async Task SaveRole(AccessRole role, bool isNew)
    {
        if (isNew) context.AccessRoles.Add(role);
        await context.SaveChangesAsync();
    }

    public async Task<List<MembershipDto>> Members()
    {
        var rows = await context.OrganizationMemberships.Join(context.Users, member => member.UserId, user => user.Id,
            (member, user) => new { Member = member, Name = user.Person.FirstName + " " + user.Person.LastName, user.Email }).ToListAsync();

        var links = await context.MembershipAccessRoles.ToListAsync();

        return rows.Select(row => new MembershipDto(row.Member.Id, row.Member.UserId, row.Name, row.Email, row.Member.IsActive, row.Member.LocationId,
        links.Where(link => link.MembershipId == row.Member.Id)
        .Select(link => link.AccessRoleId).ToList())).ToList();
    }

    public Task<OrganizationMembership?> FindMember(Guid id) => context.OrganizationMemberships.FirstOrDefaultAsync(row => row.Id == id);

    public Task<List<LocationDto>> Locations() => context.Locations.OrderBy(row => row.Name).Select(row => new LocationDto(row.Id, row.Name)).ToListAsync();

    public async Task SaveMember(OrganizationMembership membership, IReadOnlyCollection<Guid> roleIds)
    {
        var links = await context.MembershipAccessRoles.Where(row => row.MembershipId == membership.Id).ToListAsync();
        context.MembershipAccessRoles.RemoveRange(links.Where(row => !roleIds.Contains(row.AccessRoleId)));

        foreach (var roleId in roleIds.Where(id => links.All(row => row.AccessRoleId != id)))
            context.MembershipAccessRoles.Add(new() { OrganizationId = membership.OrganizationId, MembershipId = membership.Id, AccessRoleId = roleId });

        await context.SaveChangesAsync();
    }
    private IQueryable<OrganizationMembership> Administrators(Guid organizationId) =>
        context.OrganizationMemberships.Where(member => member.OrganizationId == organizationId
            && context.MembershipAccessRoles.Any(link => link.MembershipId == member.Id
                && link.OrganizationId == organizationId && context.AccessRoles.Any(role => role.Id == link.AccessRoleId
                    && role.OrganizationId == organizationId && (role.Permissions.Contains(Permissions.OrganizationsManage)
                        || role.Permissions.Contains(Permissions.MembersManage) || role.Permissions.Contains(Permissions.RolesManage)))));

    public async Task<OrganizationAdministratorsDto?> OrganizationAdministrators(Guid organizationId)
    {
        var organization = await context.Organizations.AsNoTracking().FirstOrDefaultAsync(row => row.Id == organizationId);
        if (organization == null) return null;

        var members = await Administrators(organizationId).Join(context.Users, member => member.UserId, user => user.Id,
            (member, user) => new { member.Id, member.IsActive, Name = user.Person.FirstName + " " + user.Person.LastName, user.Email }).ToListAsync();

        var grants = await context.MembershipAccessRoles.Where(row => row.OrganizationId == organizationId)
            .Join(context.AccessRoles, link => new { Id = link.AccessRoleId, link.OrganizationId },
                role => new { role.Id, role.OrganizationId }, (link, role) => new { link.MembershipId, role.Permissions }).ToListAsync();

        return new(organizationId, organization.Name, members.Select(member => new OrganizationAdministratorDto(
            member.Id, member.Name, member.Email, member.IsActive,
            grants.Where(row => row.MembershipId == member.Id).SelectMany(row => row.Permissions).Distinct().OrderBy(value => value).ToList())).ToList());
    }

    public async Task<bool> UpdateAdministrator(Guid organizationId, Guid membershipId, UpdateOrganizationAdministratorDto dto)
    {
        var member = await Administrators(organizationId).FirstOrDefaultAsync(row => row.Id == membershipId);
        if (member == null) return false;

        var user = await context.Users.FirstAsync(row => row.Id == member.UserId);
        var links = await context.MembershipAccessRoles.Where(row => row.MembershipId == membershipId && row.OrganizationId == organizationId).ToListAsync();
        var roleIds = links.Select(row => row.AccessRoleId).ToArray();
        var privateRoles = await context.AccessRoles.Where(row => row.OrganizationId == organizationId && roleIds.Contains(row.Id)
            && !context.MembershipAccessRoles.Any(link => link.AccessRoleId == row.Id && link.MembershipId != membershipId)).ToListAsync();

        // Never change a shared role when editing one administrator.
        var role = privateRoles.FirstOrDefault(row => row.Permissions.Any(Permissions.Administration.Contains));
        if (role == null)
        {
            var baseName = $"Administrator - {user.Person.FirstName} {user.Person.LastName}";
            if (baseName.Length > 85) baseName = baseName[..85];
            var name = baseName;
            var suffix = 2;
            while (await context.AccessRoles.AnyAsync(row => row.OrganizationId == organizationId && row.Name == name))
                name = $"{baseName} ({suffix++})";
            role = new AccessRole { OrganizationId = organizationId, Name = name };
            context.AccessRoles.Add(role);
        }

        role.Permissions = dto.Permissions.Distinct().ToList();
        context.MembershipAccessRoles.RemoveRange(links.Where(row => row.AccessRoleId != role.Id));

        if (links.All(row => row.AccessRoleId != role.Id))
            context.MembershipAccessRoles.Add(new() { OrganizationId = organizationId, MembershipId = membershipId, AccessRoleId = role.Id });
        member.IsActive = dto.IsActive;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAdministrator(Guid organizationId, Guid membershipId)
    {
        var member = await Administrators(organizationId).FirstOrDefaultAsync(row => row.Id == membershipId);
        if (member == null) return false;

        var links = await context.MembershipAccessRoles.Where(row => row.OrganizationId == organizationId && row.MembershipId == membershipId).ToListAsync();

        context.MembershipAccessRoles.RemoveRange(links);
        context.OrganizationMemberships.Remove(member);

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<OrganizationDeletionOutcome> DeleteOrganization(Guid organizationId)
    {
        var organization = await context.Organizations.FirstOrDefaultAsync(row => row.Id == organizationId);
        if (organization == null) return OrganizationDeletionOutcome.NotFound;

        // Platform mode cannot read clinical rows, so check only their existence here.
        if (await context.OrganizationPatients.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.Appointments.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.ClinicalRecords.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.Encounters.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.VisitHistorys.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.SessionHistories.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId))
            return OrganizationDeletionOutcome.HasHistory;

        context.SessionSlots.RemoveRange(await context.SessionSlots.IgnoreQueryFilters().Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.PractitionerSchedules.RemoveRange(await context.PractitionerSchedules.IgnoreQueryFilters().Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.PractitionerRoles.RemoveRange(await context.PractitionerRoles.IgnoreQueryFilters().Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.MembershipAccessRoles.RemoveRange(await context.MembershipAccessRoles.Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.OrganizationMemberships.RemoveRange(await context.OrganizationMemberships.Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.AccessRoles.RemoveRange(await context.AccessRoles.Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.Locations.RemoveRange(await context.Locations.Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.Organizations.Remove(organization);

        try { await context.SaveChangesAsync(); }

        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.ForeignKeyViolation })
        {
            return OrganizationDeletionOutcome.HasHistory;
        }
        return OrganizationDeletionOutcome.Deleted;
    }

}
