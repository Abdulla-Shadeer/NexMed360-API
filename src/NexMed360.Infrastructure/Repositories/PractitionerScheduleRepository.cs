using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;
using NexMed360.Domain.Scheduling;

namespace NexMed360Api.Services;

public class PractitionerScheduleRepository(ApplicationDbContext context) : IPractitionerScheduleRepository
{
    public Task<bool> PractitionerRoleExists(Guid id, Guid? organizationId = null) => context.PractitionerRoles.AnyAsync(row => row.Id == id && (!organizationId.HasValue || row.OrganizationId == organizationId));
    public Task<bool> OrganizationExists(Guid id) => context.Organizations.AnyAsync(row => row.Id == id);
    public Task<PractitionerSchedule?> Find(Guid id) => context.PractitionerSchedules.FirstOrDefaultAsync(row => row.Id == id);
    public async Task<List<PractitionerSchedule>> Get(SessionFilter filter)
    {
        var rows = await context.PractitionerSchedules.Include(row => row.History)
            .Where(row => (!filter.PractitionerRoleId.HasValue || row.PractitionerRoleId == filter.PractitionerRoleId) && (!filter.OrganizationId.HasValue || row.OrganizationId == filter.OrganizationId)
                && (!filter.Status.HasValue || row.History.Any(history => history.SessionStatus == filter.Status))).ToListAsync();
        return rows.Where(row => !filter.Date.HasValue || new SessionSchedule(row.StartTime, row.EndTime, row.Occurrences).IsDateAvailable(filter.Date.Value)).ToList();
    }
    public Task<List<PractitionerSchedule>> GetPeers(Guid practitionerRoleId, Guid organizationId, Guid? exclude = null)
        => context.PractitionerSchedules.Where(row => row.PractitionerRoleId == practitionerRoleId && row.OrganizationId == organizationId && (!exclude.HasValue || row.Id != exclude)).ToListAsync();
    public List<PractitionerSchedule> GetForPractitionerRoleOrganization(Guid practitionerRoleId, Guid organizationId)
        => context.PractitionerSchedules.Where(row => row.PractitionerRoleId == practitionerRoleId && row.OrganizationId == organizationId).ToList();
    public async Task<PractitionerSchedule> Add(PractitionerSchedule row) { await context.PractitionerSchedules.AddAsync(row); await context.SaveChangesAsync(); return row; }
    public Task<List<SessionSlot>> Slots(Guid sessionId, DateOnly date) => context.SessionSlots.Where(row => row.SessionId == sessionId && row.Date == date).ToListAsync();
    public async Task AddSlots(IEnumerable<SessionSlot> rows) { await context.SessionSlots.AddRangeAsync(rows); await context.SaveChangesAsync(); }
    public async Task AddHistory(SessionHistory row) { await context.SessionHistories.AddAsync(row); await context.SaveChangesAsync(); }
    public Task<SessionHistory?> History(Guid id, DateOnly date) => context.SessionHistories.Include(row => row.PractitionerSchedule).FirstOrDefaultAsync(row => row.PractitionerScheduleId == id && row.Date == date);
    public Task<List<SessionHistory>> Histories(SessionHistoryFilter filter) => context.SessionHistories.Include(row => row.PractitionerSchedule)
        .Where(row => (!filter.SessionId.HasValue || row.PractitionerScheduleId == filter.SessionId) && (!filter.PractitionerRoleId.HasValue || row.PractitionerSchedule.PractitionerRoleId == filter.PractitionerRoleId)
            && (!filter.OrganizationId.HasValue || row.PractitionerSchedule.OrganizationId == filter.OrganizationId) && (!filter.Status.HasValue || row.SessionStatus == filter.Status)
            && (!filter.FromDate.HasValue || row.Date > filter.FromDate) && (!filter.ToDate.HasValue || row.Date < filter.ToDate)).ToListAsync();
    public Task<SessionHistory?> Current(Guid practitionerRoleId, Guid organizationId, DateOnly date) => context.SessionHistories.Include(row => row.PractitionerSchedule)
        .FirstOrDefaultAsync(row => row.SessionStatus == SessionStatus.Started && row.Date == date && row.PractitionerSchedule.PractitionerRoleId == practitionerRoleId && row.PractitionerSchedule.OrganizationId == organizationId);
    public Task<List<Guid>> ClosedSessionIds(DateOnly date) => context.PractitionerSchedules.Where(row => row.History.Any(history => history.Date == date
        && (history.SessionStatus == SessionStatus.Canceled || history.SessionStatus == SessionStatus.Ended))).Select(row => row.Id).ToListAsync();
    public Task<Guid> OrganizationMemberOrganization(string userId) => context.OrganizationMemberships.Where(row => row.UserId == userId).Select(row => row.OrganizationId).FirstOrDefaultAsync();
    public Task<List<SessionHistory>> Ongoing(Guid organizationId, DateOnly date) => context.SessionHistories.Include(row => row.PractitionerSchedule)
        .Where(row => row.PractitionerSchedule.OrganizationId == organizationId && row.SessionStatus == SessionStatus.Started && row.Date == date).ToListAsync();
    public async Task<bool> CanDelete(Guid id) => !await context.SessionSlots.AnyAsync(row => row.SessionId == id)
        || await context.SessionSlots.AllAsync(row => row.SessionId == id && row.IsAvailable);
    public void Remove(PractitionerSchedule row) => context.PractitionerSchedules.Remove(row);
    public async Task SaveChanges() => await context.SaveChangesAsync();
}
