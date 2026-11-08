using NexMed360Api.Common.Auth;
namespace NexMed360.Application.Access;

// This state belongs to one request. Grants are loaded again on the next request.
public sealed class WorkspaceAccess
{
    public WorkspaceScope? Current { get; private set; }
    public bool IsSelected => Current != null;
    public string? Mode => Current?.Mode;
    public Guid? OrganizationId => Current?.OrganizationId;
    public Guid? PatientId => Current?.PatientId;
    public Guid? PractitionerRoleId => Current?.PractitionerRoleId;
    public bool HasLocationScope => Current?.LocationIds != null;
    public List<Guid> LocationIds => Current?.LocationIds ?? [];
    public bool Has(string permission) => Current?.Permissions.Contains(permission) == true;
    public void Select(WorkspaceScope workspace) => Current = workspace;
    public void RequireOrganization(Guid organizationId)
    {
        if (IsSelected && Mode == WorkspaceModes.Work && OrganizationId != organizationId)
            throw new UnauthorizedAccessException("Choose a record from your current organization.");
    }
    public void RequireAssignment(Guid assignmentId)
    {
        if (IsSelected && PractitionerRoleId != assignmentId)
            throw new UnauthorizedAccessException("Choose your current practitioner assignment.");
    }
}

public sealed class WorkspaceScope
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "";
    public Guid? OrganizationId { get; set; }
    public Guid? MembershipId { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? PractitionerRoleId { get; set; }
    public string? OrganizationName { get; set; }
    public List<Guid>? LocationIds { get; set; }
    public List<string> Permissions { get; set; } = [];
}

public sealed record SaveAccessRoleDto(string Name, List<string> Permissions);
public sealed record UpdateMembershipDto(bool IsActive, List<Guid> AccessRoleIds, Guid? LocationId);
public sealed record AccessRoleDto(Guid Id, string Name, List<string> Permissions);
public sealed record MembershipDto(Guid Id, string UserId, string Name, string? Email, bool IsActive,
    Guid? LocationId, List<Guid> AccessRoleIds);
public sealed record LocationDto(Guid Id, string Name);
