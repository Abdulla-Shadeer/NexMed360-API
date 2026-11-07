using NexMed360Api.Entities;

namespace NexMed360.Application.Shared;

public sealed record AccountSummary(string Id, string? FirstName, string? LastName, string? Email, string? PhoneNumber)
{
    public Guid PersonId { get; init; }
    public List<string> Roles { get; init; } = [];
    public bool IncludeProfiles { get; init; } = true;
    public string FullName => $"{FirstName} {LastName}";
}

public sealed class PortalReadRequest
{
    public List<Guid> PatientIds { get; } = [];
    public List<Guid> PractitionerRoleIds { get; } = [];
    public List<Guid> OrganizationIds { get; } = [];
    public List<Guid> SessionIds { get; } = [];
    public List<Guid> SlotIds { get; } = [];
    public List<Guid> PersonIds { get; } = [];
    public List<string> AccountIds { get; } = [];
    public List<string> ProfileUserIds { get; } = [];
    public bool IncludeOrganizationManagers { get; init; }
    public bool IncludeAccountRoles { get; init; }
}

public sealed class PortalReadData
{
    public Dictionary<Guid, Patient> Patients { get; init; } = [];
    public Dictionary<Guid, Practitioner> Practitioners { get; init; } = [];
    public Dictionary<Guid, PractitionerRole> PractitionerRoles { get; init; } = [];
    public Dictionary<Guid, Organization> Organizations { get; init; } = [];
    public Dictionary<Guid, PractitionerSchedule> Sessions { get; init; } = [];
    public Dictionary<Guid, SessionSlot> Slots { get; init; } = [];
    public Dictionary<Guid, Location> Locations { get; init; } = [];
    public Dictionary<string, AccountSummary> Accounts { get; init; } = [];
    public Dictionary<Guid, AccountSummary> People { get; init; } = [];
    public List<OrganizationMembership> Memberships { get; init; } = [];
}

public interface IPortalReadStore
{
    Task<PortalReadData> Load(PortalReadRequest request);
}
