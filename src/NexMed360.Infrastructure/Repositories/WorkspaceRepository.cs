using NexMed360Api.Common.Auth;
using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Access;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Repositories;

public sealed class WorkspaceRepository(ApplicationDbContext context) : IWorkspaceRepository
{
    public async Task<List<WorkspaceOptionDto>> List(string userId)
    {
        var user = await context.Users.AsNoTracking().Where(row => row.Id == userId && row.IsActive)
            .Select(row => new { row.PersonId, row.IsPlatformAdmin }).FirstOrDefaultAsync();
        if (user == null) return [];

        List<WorkspaceOptionDto> result = [];
        if (await context.Patients.IgnoreQueryFilters().AnyAsync(row => row.PersonId == user.PersonId))
            result.Add(new(WorkspaceModes.Patient, "Patient mode", WorkspaceModes.Patient));
        if (user.IsPlatformAdmin) result.Add(new(WorkspaceModes.Platform, "Platform Admin mode", WorkspaceModes.Platform));

        // Workspace choices must be found before a workspace filter can be selected.
        var memberQuery =
            from member in context.OrganizationMembers.IgnoreQueryFilters().AsNoTracking()
            join organization in context.Organizations on member.OrganizationId equals organization.Id
            where member.UserId == userId && member.IsActive
            select new { Member = member, OrganizationName = organization.Name };

        var memberDetails = await memberQuery.ToListAsync();

        foreach (var details in memberDetails)
            result.Add(new($"{WorkspaceModes.Work}:{details.Member.OrganizationId}", $"{details.OrganizationName} — Work mode", WorkspaceModes.Work));

        return result;
    }

    public async Task<WorkspaceScope?> Resolve(string userId, string key)
    {
        var user = await context.Users.AsNoTracking().Where(row => row.Id == userId && row.IsActive)
            .Select(row => new { row.PersonId, row.IsPlatformAdmin }).FirstOrDefaultAsync();
        if (user == null) return null;

        if (key == WorkspaceModes.Patient)
        {
            var patientId = await context.Patients.IgnoreQueryFilters().Where(row => row.PersonId == user.PersonId).Select(row => (Guid?)row.Id).FirstOrDefaultAsync();
            return patientId.HasValue ? new() { Key = key, Name = "Patient mode", Mode = WorkspaceModes.Patient, PatientId = patientId } : null;
        }
        if (key == WorkspaceModes.Platform)
            return user.IsPlatformAdmin ? new() { Key = key, Name = "Platform Admin mode", Mode = WorkspaceModes.Platform } : null;

        var parts = key.Split(':');
        if (parts.Length != 2 || parts[0] != WorkspaceModes.Work || !Guid.TryParse(parts[1], out var organizationId)) return null;

        // Resolve fresh grants across filters; every read is tied to this account and organization.
        var member = await context.OrganizationMembers.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserId == userId && row.OrganizationId == organizationId && row.IsActive);
        if (member == null) return null;

        var organization = await context.Organizations.AsNoTracking().FirstOrDefaultAsync(row => row.Id == organizationId);
        if (organization == null) return null;

        var locations = await Locations(organizationId);
        var scope = member.LocationId.HasValue ? Descendants(locations, member.LocationId.Value) : null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignmentQuery =
            from role in context.PractitionerRoles.IgnoreQueryFilters().AsNoTracking()
            join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            where practitioner.PersonId == user.PersonId && role.OrganizationId == organizationId && role.IsActive
                && (!role.StartDate.HasValue || role.StartDate <= today)
                && (!role.EndDate.HasValue || role.EndDate >= today)
            select role;

        var assignments = (await assignmentQuery.ToListAsync())
            .Where(role => scope == null || role.LocationId.HasValue && scope.Contains(role.LocationId.Value)).ToList();

        // Old duplicate rows keep their history, but need only one clinical choice.
        var choices = assignments.OrderBy(role => role.Id).DistinctBy(role => new
        {
            role.LocationId, Name = role.RoleName.Trim().ToUpperInvariant(),
            Specialty = role.Specialty.Trim().ToUpperInvariant(), role.StartDate, role.EndDate
        }).Select(role => new PractitionerAssignmentDto(role.Id, AssignmentName(role, locations))).ToList();

        var grantQuery =
            from roleAssignment in context.MemberRoleAssignments.IgnoreQueryFilters()
            join role in context.AccessRoles.IgnoreQueryFilters()
                on new { Id = roleAssignment.AccessRoleId, roleAssignment.OrganizationId }
                equals new { role.Id, role.OrganizationId }
            where roleAssignment.OrganizationMemberId == member.Id && roleAssignment.OrganizationId == organizationId
            select role.Permissions;

        var grants = await grantQuery.ToListAsync();

        return new()
        {
            Key = key, Name = $"{organization.Name} — Work mode", Mode = WorkspaceModes.Work, OrganizationId = organizationId,
            OrganizationName = organization.Name, OrganizationMemberId = member.Id, PractitionerRoleId = choices.Count == 1 ? choices[0].Id : null,
            PractitionerAssignments = choices, PractitionerRoleIds = assignments.Select(role => role.Id).ToList(),
            LocationIds = scope, Permissions = grants.SelectMany(permissions => permissions)
                .Distinct().OrderBy(value => value).ToList()
        };
    }

    private Task<List<Location>> Locations(Guid organizationId) => context.Locations.IgnoreQueryFilters().AsNoTracking()
        .Where(row => row.OrganizationId == organizationId).ToListAsync();

    private static string AssignmentName(PractitionerRole role, List<Location> locations)
    {
        var name = role.RoleName;
        if (!string.IsNullOrWhiteSpace(role.Specialty)) name += $" ({role.Specialty})";
        var location = locations.FirstOrDefault(row => row.Id == role.LocationId);
        if (location != null) name += $" — {location.Name}";
        return name;
    }

    private static List<Guid> Descendants(List<Location> locations, Guid root)
    {
        var ids = new HashSet<Guid> { root };
        while (true)
        {
            var children = locations.Where(row => row.ParentId.HasValue && ids.Contains(row.ParentId.Value) && !ids.Contains(row.Id)).ToList();
            if (children.Count == 0) return ids.OrderBy(id => id).ToList();
            foreach (var child in children) ids.Add(child.Id);
        }
    }
}
