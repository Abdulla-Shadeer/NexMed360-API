using NexMed360Api.Entities;

namespace NexMed360.Application.Shared;

public sealed record AccountSummary(string Id, string? FirstName, string? LastName, string? Email, string? PhoneNumber)
{
    public List<string> Roles { get; init; } = [];
    public bool IncludeProfiles { get; init; } = true;
    public string FullName => $"{FirstName} {LastName}";
}

public sealed class PortalReadRequest
{
    public List<Guid> PatientIds { get; } = [];
    public List<Guid> DoctorIds { get; } = [];
    public List<Guid> HospitalIds { get; } = [];
    public List<Guid> SessionIds { get; } = [];
    public List<Guid> SlotIds { get; } = [];
    public List<string> AccountIds { get; } = [];
    public List<string> ProfileUserIds { get; } = [];
    public bool IncludeHospitalAdmins { get; init; }
    public bool IncludeAccountRoles { get; init; }
}

public sealed class PortalReadData
{
    public Dictionary<Guid, Patient> Patients { get; init; } = [];
    public Dictionary<Guid, Doctor> Doctors { get; init; } = [];
    public Dictionary<Guid, Hospital> Hospitals { get; init; } = [];
    public Dictionary<Guid, DoctorSession> Sessions { get; init; } = [];
    public Dictionary<Guid, SessionSlot> Slots { get; init; } = [];
    public Dictionary<string, AccountSummary> Accounts { get; init; } = [];
    public List<HospitalAdmin> Admins { get; init; } = [];
    public List<Receptionist> Receptionists { get; init; } = [];
}

public interface IPortalReadStore
{
    Task<PortalReadData> Load(PortalReadRequest request);
}
