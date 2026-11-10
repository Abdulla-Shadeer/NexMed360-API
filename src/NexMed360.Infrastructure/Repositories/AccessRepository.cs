using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Access;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Repositories;

public sealed class AccessRepository(ApplicationDbContext context) : IAccessRepository
{
    public Task<List<AccessRole>> Roles(Guid? organizationId = null) => context.AccessRoles
        .Where(role => !organizationId.HasValue || role.OrganizationId == organizationId)
        .OrderBy(role => role.Name).ToListAsync();

    public Task<AccessRole?> FindRole(Guid id) => context.AccessRoles.FirstOrDefaultAsync(row => row.Id == id);

    public Task<bool> RoleNameExists(Guid organizationId, string name, Guid? exceptId = null) => context.AccessRoles.AnyAsync(row => row.OrganizationId == organizationId && row.Name == name && row.Id != exceptId);

    public async Task SaveRole(AccessRole role, bool isNew)
    {
        if (isNew) context.AccessRoles.Add(role);
        await context.SaveChangesAsync();
    }

    public Task<List<MembershipDto>> Members(Guid? organizationId = null)
    {
        var members =
            from member in context.OrganizationMemberships
            join user in context.Users on member.UserId equals user.Id
            join person in context.People on user.PersonId equals person.Id
            where !organizationId.HasValue || member.OrganizationId == organizationId
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

    public Task<List<AccessRole>> MemberRoles(Guid organizationId, Guid membershipId)
    {
        var roles =
            from assignment in context.MembershipAccessRoles
            join role in context.AccessRoles
                on new { Id = assignment.AccessRoleId, assignment.OrganizationId }
                equals new { role.Id, role.OrganizationId }
            where assignment.OrganizationId == organizationId && assignment.MembershipId == membershipId
            select role;

        return roles.ToListAsync();
    }

    public Task<bool> RoleIsShared(Guid roleId, Guid membershipId) => context.MembershipAccessRoles
        .AnyAsync(assignment => assignment.AccessRoleId == roleId && assignment.MembershipId != membershipId);

    public Task<string> MemberName(string userId)
    {
        var names =
            from user in context.Users
            join person in context.People on user.PersonId equals person.Id
            where user.Id == userId
            select person.FirstName + " " + person.LastName;

        return names.FirstAsync();
    }

    public async Task SaveAdministrator(OrganizationMembership member, AccessRole role, bool isNewRole)
    {
        if (isNewRole) context.AccessRoles.Add(role);
        // Save the selected role and membership links together.
        await SaveMember(member, [role.Id]);
    }

    public async Task RemoveMember(OrganizationMembership member)
    {
        await context.Entry(member).Collection(row => row.RoleAssignments).LoadAsync();
        context.OrganizationMemberships.Remove(member);
        await context.SaveChangesAsync();
    }

    public Task<Organization?> FindOrganization(Guid organizationId) =>
        context.Organizations.FirstOrDefaultAsync(row => row.Id == organizationId);

    public async Task<bool> HasPatientHistory(Guid organizationId)
    {
        // Check existence only; platform mode cannot read clinical rows through normal queries.
        return await context.OrganizationPatients.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.Appointments.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.ClinicalRecords.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.Encounters.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.VisitHistorys.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId)
            || await context.SessionHistories.IgnoreQueryFilters().AnyAsync(row => row.OrganizationId == organizationId);
    }

    public async Task<bool> DeleteOrganization(Organization organization)
    {
        var organizationId = organization.Id;
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
            return false;
        }

        return true;
    }
}
