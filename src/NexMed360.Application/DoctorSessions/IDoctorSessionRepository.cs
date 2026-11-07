using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Interfaces.Services;
public sealed record SessionFilter(Guid? DoctorId, Guid? HospitalId, DateOnly? Date, SessionStatus? Status);
public sealed record SessionHistoryFilter(Guid? SessionId, Guid? DoctorId, Guid? HospitalId, SessionStatus? Status, DateOnly? FromDate, DateOnly? ToDate);
public interface IDoctorSessionRepository
{
    Task<bool> DoctorExists(Guid id);
    Task<bool> HospitalExists(Guid id);
    Task<DoctorSession?> Find(Guid id);
    Task<List<DoctorSession>> Get(SessionFilter filter);
    Task<List<DoctorSession>> GetPeers(Guid doctorId, Guid hospitalId, Guid? exclude = null);
    List<DoctorSession> GetForDoctorHospital(Guid doctorId, Guid hospitalId);
    Task<DoctorSession> Add(DoctorSession row);
    Task<List<SessionSlot>> Slots(Guid sessionId, DateOnly date);
    Task AddSlots(IEnumerable<SessionSlot> rows);
    Task AddHistory(SessionHistory row);
    Task<SessionHistory?> History(Guid sessionId, DateOnly date);
    Task<List<SessionHistory>> Histories(SessionHistoryFilter filter);
    Task<SessionHistory?> Current(Guid doctorId, Guid hospitalId, DateOnly date);
    Task<List<Guid>> ClosedSessionIds(DateOnly date);
    Task<Guid> ReceptionistHospital(string userId);
    Task<List<SessionHistory>> Ongoing(Guid hospitalId, DateOnly date);
    Task<bool> CanDelete(Guid id);
    void Remove(DoctorSession row);
    Task SaveChanges();
}
