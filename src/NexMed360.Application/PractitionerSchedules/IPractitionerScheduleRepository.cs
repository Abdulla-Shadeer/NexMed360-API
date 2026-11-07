using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Interfaces.Services;
public sealed record SessionFilter(Guid? PractitionerRoleId, Guid? OrganizationId, DateOnly? Date, SessionStatus? Status);
public sealed record SessionHistoryFilter(Guid? SessionId, Guid? PractitionerRoleId, Guid? OrganizationId, SessionStatus? Status, DateOnly? FromDate, DateOnly? ToDate);
public interface IPractitionerScheduleRepository
{
    Task<bool> PractitionerRoleExists(Guid id, Guid? organizationId = null);
    Task<bool> OrganizationExists(Guid id);
    Task<PractitionerSchedule?> Find(Guid id);
    Task<List<PractitionerSchedule>> Get(SessionFilter filter);
    Task<List<PractitionerSchedule>> GetPeers(Guid practitionerRoleId, Guid organizationId, Guid? exclude = null);
    List<PractitionerSchedule> GetForPractitionerRoleOrganization(Guid practitionerRoleId, Guid organizationId);
    Task<PractitionerSchedule> Add(PractitionerSchedule row);
    Task<List<SessionSlot>> Slots(Guid sessionId, DateOnly date);
    Task AddSlots(IEnumerable<SessionSlot> rows);
    Task AddHistory(SessionHistory row);
    Task<SessionHistory?> History(Guid sessionId, DateOnly date);
    Task<List<SessionHistory>> Histories(SessionHistoryFilter filter);
    Task<SessionHistory?> Current(Guid practitionerRoleId, Guid organizationId, DateOnly date);
    Task<List<Guid>> ClosedSessionIds(DateOnly date);
    Task<Guid> OrganizationMemberOrganization(string userId);
    Task<List<SessionHistory>> Ongoing(Guid organizationId, DateOnly date);
    Task<bool> CanDelete(Guid id);
    void Remove(PractitionerSchedule row);
    Task SaveChanges();
}
