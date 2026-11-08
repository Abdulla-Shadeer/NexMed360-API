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
            result.Add(new(WorkspaceModes.Patient, "My patient account", WorkspaceModes.Patient));
        if (user.IsPlatformAdmin) result.Add(new(WorkspaceModes.Platform, "Platform administration", WorkspaceModes.Platform));
        var memberships = await (from member in context.OrganizationMemberships.IgnoreQueryFilters().AsNoTracking()
            join organization in context.Organizations on member.OrganizationId equals organization.Id
            where member.UserId == userId && member.IsActive
            select new { Member = member, organization.Name }).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignments = await context.PractitionerRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(role => role.IsActive && (!role.StartDate.HasValue || role.StartDate <= today) && (!role.EndDate.HasValue || role.EndDate >= today)
                && context.Practitioners.Any(practitioner => practitioner.Id == role.PractitionerId && practitioner.PersonId == user.PersonId)).ToListAsync();
        foreach (var row in memberships)
        {
            var member = row.Member;
            result.Add(new($"{WorkspaceModes.Work}:{member.OrganizationId}", Name(row.Name, null), WorkspaceModes.Work));
            var locations = member.LocationId.HasValue ? await Locations(member.OrganizationId) : [];
            var scope = member.LocationId.HasValue ? Descendants(locations, member.LocationId.Value) : null;
            foreach (var assignment in assignments.Where(role => role.OrganizationId == member.OrganizationId
                && (scope == null || role.LocationId.HasValue && scope.Contains(role.LocationId.Value))))
                result.Add(new($"{WorkspaceModes.Work}:{member.OrganizationId}:{assignment.Id}", Name(row.Name, assignment), WorkspaceModes.Work));
        }
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
            return patientId.HasValue ? new() { Key = key, Name = "My patient account", Mode = WorkspaceModes.Patient, PatientId = patientId } : null;
        }
        if (key == WorkspaceModes.Platform)
            return user.IsPlatformAdmin ? new() { Key = key, Name = "Platform administration", Mode = WorkspaceModes.Platform } : null;
        var parts = key.Split(':');
        if (parts.Length is not (2 or 3) || parts[0] != WorkspaceModes.Work || !Guid.TryParse(parts[1], out var organizationId)) return null;
        Guid? assignmentId = null;
        if (parts.Length == 3)
        {
            if (!Guid.TryParse(parts[2], out var id)) return null;
            assignmentId = id;
        }
        var member = await context.OrganizationMemberships.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserId == userId && row.OrganizationId == organizationId && row.IsActive);
        if (member == null) return null;
        var organization = await context.Organizations.AsNoTracking().FirstOrDefaultAsync(row => row.Id == organizationId);
        if (organization == null) return null;
        var locations = await Locations(organizationId);
        var scope = member.LocationId.HasValue ? Descendants(locations, member.LocationId.Value) : null;
        PractitionerRole? assignment = null;
        if (assignmentId.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            assignment = await context.PractitionerRoles.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(role =>
                role.Id == assignmentId && role.OrganizationId == organizationId && role.IsActive
                && (!role.StartDate.HasValue || role.StartDate <= today) && (!role.EndDate.HasValue || role.EndDate >= today)
                && context.Practitioners.Any(practitioner => practitioner.Id == role.PractitionerId && practitioner.PersonId == user.PersonId));
            if (assignment == null || scope != null && (!assignment.LocationId.HasValue || !scope.Contains(assignment.LocationId.Value))) return null;
            if (assignment.LocationId.HasValue) scope = Descendants(locations, assignment.LocationId.Value);
        }
        var grants = await context.MembershipAccessRoles.IgnoreQueryFilters()
            .Where(link => link.MembershipId == member.Id && link.OrganizationId == organizationId)
            .Join(context.AccessRoles.IgnoreQueryFilters(), link => new { Id = link.AccessRoleId, link.OrganizationId },
                role => new { role.Id, role.OrganizationId }, (link, role) => role.Permissions).ToListAsync();
        return new()
        {
            Key = key, Name = Name(organization.Name, assignment), Mode = WorkspaceModes.Work, OrganizationId = organizationId,
            OrganizationName = organization.Name, MembershipId = member.Id, PractitionerRoleId = assignment?.Id,
            LocationIds = scope, Permissions = grants.SelectMany(row => row).Distinct().OrderBy(value => value).ToList()
        };
    }

    private Task<List<Location>> Locations(Guid organizationId) => context.Locations.IgnoreQueryFilters().AsNoTracking()
        .Where(row => row.OrganizationId == organizationId).ToListAsync();
    private static string Name(string organization, PractitionerRole? assignment) => organization +
        (assignment == null ? " — Organization work" : $" — {assignment.RoleName} ({assignment.Specialty})");
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
