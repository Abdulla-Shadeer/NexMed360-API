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

    public Task<List<MembershipDto>> Members()
    {
        var members =
            from member in context.OrganizationMemberships
            join user in context.Users on member.UserId equals user.Id
            join person in context.People on user.PersonId equals person.Id
            select new MembershipDto(
                member.Id,
                member.UserId,
                person.FirstName + " " + person.LastName,
                user.Email,
                member.IsActive,
                member.LocationId,
                member.RoleAssignments.Select(assignment => assignment.AccessRoleId).ToList());

        return members.ToListAsync();
    }

    public Task<OrganizationMembership?> FindMember(Guid id) => context.OrganizationMemberships.FirstOrDefaultAsync(row => row.Id == id);

    public Task<List<LocationDto>> Locations() => context.Locations.OrderBy(row => row.Name).Select(row => new LocationDto(row.Id, row.Name)).ToListAsync();

    public async Task SaveMember(OrganizationMembership membership, IReadOnlyCollection<Guid> roleIds)
    {
        await context.Entry(membership).Collection(member => member.RoleAssignments).LoadAsync();
        var links = membership.RoleAssignments;
        context.MembershipAccessRoles.RemoveRange(links.Where(row => !roleIds.Contains(row.AccessRoleId)));

        foreach (var roleId in roleIds.Where(id => links.All(row => row.AccessRoleId != id)))
        {
            membership.RoleAssignments.Add(new() { OrganizationId = membership.OrganizationId, MembershipId = membership.Id, AccessRoleId = roleId });
        }

        await context.SaveChangesAsync();
    }

    // Administrative permissions identify administrators, regardless of role names or active status.
    private IQueryable<OrganizationMembership> Administrators(Guid organizationId)
    {
        var administratorIds =
            from assignment in context.MembershipAccessRoles
            join role in context.AccessRoles
                on new { Id = assignment.AccessRoleId, assignment.OrganizationId }
                equals new { role.Id, role.OrganizationId }
            where assignment.OrganizationId == organizationId
                && (role.Permissions.Contains(Permissions.OrganizationsManage)
                    || role.Permissions.Contains(Permissions.MembersManage)
                    || role.Permissions.Contains(Permissions.RolesManage))
            select assignment.MembershipId;

        return context.OrganizationMemberships.Where(member => member.OrganizationId == organizationId
            && administratorIds.Contains(member.Id));
    }

    public async Task<OrganizationAdministratorsDto?> OrganizationAdministrators(Guid organizationId)
    {
        var organization = await context.Organizations.AsNoTracking().FirstOrDefaultAsync(row => row.Id == organizationId);
        if (organization == null) return null;

        var memberQuery =
            from member in Administrators(organizationId)
            join user in context.Users on member.UserId equals user.Id
            join person in context.People on user.PersonId equals person.Id
            select new { member.Id, member.IsActive, Name = person.FirstName + " " + person.LastName, user.Email };

        var members = await memberQuery.ToListAsync();

        var grantQuery =
            from assignment in context.MembershipAccessRoles
            join role in context.AccessRoles
                on new { Id = assignment.AccessRoleId, assignment.OrganizationId }
                equals new { role.Id, role.OrganizationId }
            where assignment.OrganizationId == organizationId
            select new { assignment.MembershipId, role.Permissions };

        var grants = await grantQuery.ToListAsync();

        return new(organizationId, organization.Name, members.Select(member => new OrganizationAdministratorDto(
            member.Id, member.Name, member.Email, member.IsActive,
            grants.Where(grant => grant.MembershipId == member.Id)
                .SelectMany(grant => grant.Permissions).Distinct().OrderBy(value => value).ToList())).ToList());
    }

    public async Task<bool> UpdateAdministrator(Guid organizationId, Guid membershipId, UpdateOrganizationAdministratorDto dto)
    {
        var member = await Administrators(organizationId)
            .Include(row => row.RoleAssignments)
            .FirstOrDefaultAsync(row => row.Id == membershipId);
        if (member == null) return false;

        var links = member.RoleAssignments;
        var roleIds = links.Select(link => link.AccessRoleId).ToArray();
        var rolesAssignedElsewhere = context.MembershipAccessRoles
            .Where(link => link.OrganizationId == organizationId && link.MembershipId != membershipId)
            .Select(link => link.AccessRoleId);

        var privateRoles = await context.AccessRoles.Where(role => role.OrganizationId == organizationId
            && roleIds.Contains(role.Id) && !rolesAssignedElsewhere.Contains(role.Id)).ToListAsync();

        // Never change a shared role when editing one administrator.
        var role = privateRoles.FirstOrDefault(row => row.Permissions.Any(Permissions.Administration.Contains));
        if (role == null)
        {
            var nameQuery =
                from user in context.Users
                join person in context.People on user.PersonId equals person.Id
                where user.Id == member.UserId
                select person.FirstName + " " + person.LastName;

            var baseName = $"Administrator - {await nameQuery.FirstAsync()}";
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
            member.RoleAssignments.Add(new() { OrganizationId = organizationId, MembershipId = membershipId, AccessRoleId = role.Id });

        member.IsActive = dto.IsActive;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAdministrator(Guid organizationId, Guid membershipId)
    {
        var member = await Administrators(organizationId).Include(row => row.RoleAssignments)
            .FirstOrDefaultAsync(row => row.Id == membershipId);
        if (member == null) return false;

        // Role assignments belong to the membership and are deleted by its cascade.
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

        context.OrganizationMemberships.RemoveRange(await context.OrganizationMemberships
            .Include(member => member.RoleAssignments).Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.AccessRoles.RemoveRange(await context.AccessRoles.Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.Locations.RemoveRange(await context.Locations.Where(row => row.OrganizationId == organizationId).ToListAsync());

        context.Organizations.Remove(organization);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.ForeignKeyViolation })
        {
            return OrganizationDeletionOutcome.HasHistory;
        }

        return OrganizationDeletionOutcome.Deleted;
    }
}
