namespace NexMed360.Application.Access;

public sealed record WorkspaceOptionDto(string Key, string Name, string Mode);

public sealed record WorkspaceDto(string Key, string Name, string Mode, Guid? OrganizationId,
    string? OrganizationName, Guid? OrganizationMemberId, Guid? PatientId, Guid? PractitionerRoleId,
    bool IsLocationRestricted, List<string> Permissions)
{
    public List<PractitionerAssignmentDto> PractitionerAssignments { get; init; } = [];

    public static WorkspaceDto From(WorkspaceScope scope) => new(scope.Key, scope.Name, scope.Mode,
        scope.OrganizationId, scope.OrganizationName, scope.OrganizationMemberId, scope.PatientId,
        scope.PractitionerRoleId, scope.LocationIds != null, scope.Permissions) { PractitionerAssignments = scope.PractitionerAssignments };
}
