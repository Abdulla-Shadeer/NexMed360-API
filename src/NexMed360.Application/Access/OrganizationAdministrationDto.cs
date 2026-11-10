namespace NexMed360.Application.Access;

public sealed record OrganizationAdministratorsDto(Guid OrganizationId, string OrganizationName,
    List<OrganizationAdministratorDto> Administrators);
public sealed record OrganizationAdministratorDto(Guid Id, string Name, string? Email, bool IsActive, List<string> Permissions);
public sealed record UpdateOrganizationAdministratorDto(bool IsActive, List<string> Permissions);
