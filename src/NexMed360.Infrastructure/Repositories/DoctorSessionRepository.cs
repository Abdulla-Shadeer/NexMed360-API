using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;
using NexMed360.Domain.Scheduling;

namespace NexMed360Api.Services;

public class DoctorSessionRepository(ApplicationDbContext context) : IDoctorSessionRepository
{
    public Task<bool> DoctorExists(Guid id) => context.Doctors.AnyAsync(row => row.Id == id);
    public Task<bool> HospitalExists(Guid id) => context.Hospitals.AnyAsync(row => row.Id == id);
    public Task<DoctorSession?> Find(Guid id) => context.DoctorSessions.FirstOrDefaultAsync(row => row.Id == id);
    public async Task<List<DoctorSession>> Get(SessionFilter filter)
    {
        var rows = await context.DoctorSessions.Include(row => row.History)
            .Where(row => (!filter.DoctorId.HasValue || row.DoctorId == filter.DoctorId) && (!filter.HospitalId.HasValue || row.HospitalId == filter.HospitalId)
                && (!filter.Status.HasValue || row.History.Any(history => history.SessionStatus == filter.Status))).ToListAsync();
        return rows.Where(row => !filter.Date.HasValue || new SessionSchedule(row.StartTime, row.EndTime, row.Occurrences).IsDateAvailable(filter.Date.Value)).ToList();
    }
    public Task<List<DoctorSession>> GetPeers(Guid doctorId, Guid hospitalId, Guid? exclude = null)
        => context.DoctorSessions.Where(row => row.DoctorId == doctorId && row.HospitalId == hospitalId && (!exclude.HasValue || row.Id != exclude)).ToListAsync();
    public List<DoctorSession> GetForDoctorHospital(Guid doctorId, Guid hospitalId)
        => context.DoctorSessions.Where(row => row.DoctorId == doctorId && row.HospitalId == hospitalId).ToList();
    public async Task<DoctorSession> Add(DoctorSession row) { await context.DoctorSessions.AddAsync(row); await context.SaveChangesAsync(); return row; }
    public Task<List<SessionSlot>> Slots(Guid sessionId, DateOnly date) => context.SessionSlots.Where(row => row.SessionId == sessionId && row.Date == date).ToListAsync();
    public async Task AddSlots(IEnumerable<SessionSlot> rows) { await context.SessionSlots.AddRangeAsync(rows); await context.SaveChangesAsync(); }
    public async Task AddHistory(SessionHistory row) { await context.SessionHistories.AddAsync(row); await context.SaveChangesAsync(); }
    public Task<SessionHistory?> History(Guid id, DateOnly date) => context.SessionHistories.Include(row => row.DoctorSession).FirstOrDefaultAsync(row => row.DoctorSessionId == id && row.Date == date);
    public Task<List<SessionHistory>> Histories(SessionHistoryFilter filter) => context.SessionHistories.Include(row => row.DoctorSession)
        .Where(row => (!filter.SessionId.HasValue || row.DoctorSessionId == filter.SessionId) && (!filter.DoctorId.HasValue || row.DoctorSession.DoctorId == filter.DoctorId)
            && (!filter.HospitalId.HasValue || row.DoctorSession.HospitalId == filter.HospitalId) && (!filter.Status.HasValue || row.SessionStatus == filter.Status)
            && (!filter.FromDate.HasValue || row.Date > filter.FromDate) && (!filter.ToDate.HasValue || row.Date < filter.ToDate)).ToListAsync();
    public Task<SessionHistory?> Current(Guid doctorId, Guid hospitalId, DateOnly date) => context.SessionHistories.Include(row => row.DoctorSession)
        .FirstOrDefaultAsync(row => row.SessionStatus == SessionStatus.Started && row.Date == date && row.DoctorSession.DoctorId == doctorId && row.DoctorSession.HospitalId == hospitalId);
    public Task<List<Guid>> ClosedSessionIds(DateOnly date) => context.DoctorSessions.Where(row => row.History.Any(history => history.Date == date
        && (history.SessionStatus == SessionStatus.Canceled || history.SessionStatus == SessionStatus.Ended))).Select(row => row.Id).ToListAsync();
    public Task<Guid> ReceptionistHospital(string userId) => context.Receptionists.Where(row => row.UserId == userId).Select(row => row.HospitalId).FirstOrDefaultAsync();
    public Task<List<SessionHistory>> Ongoing(Guid hospitalId, DateOnly date) => context.SessionHistories.Include(row => row.DoctorSession)
        .Where(row => row.DoctorSession.HospitalId == hospitalId && row.SessionStatus == SessionStatus.Started && row.Date == date).ToListAsync();
    public async Task<bool> CanDelete(Guid id) => !await context.SessionSlots.AnyAsync(row => row.SessionId == id)
        || await context.SessionSlots.AllAsync(row => row.SessionId == id && row.IsAvailable);
    public void Remove(DoctorSession row) => context.DoctorSessions.Remove(row);
    public async Task SaveChanges() => await context.SaveChangesAsync();
}
