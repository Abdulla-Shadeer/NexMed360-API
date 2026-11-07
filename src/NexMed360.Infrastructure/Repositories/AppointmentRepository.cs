using NexMed360.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;

public class AppointmentRepository(ApplicationDbContext context) : IAppointmentRepository
{
    public async Task<SessionSlot?> FindSlot(Guid id)
    {
        var slot = await context.SessionSlots.FirstOrDefaultAsync(row => row.Id == id);
        if (slot != null) slot.Session = (await context.PractitionerSchedules.FirstOrDefaultAsync(row => row.Id == slot.SessionId))!;
        return slot;
    }
    public Task<Appointment?> Find(Guid id) => context.Appointments.FirstOrDefaultAsync(row => row.Id == id);
    public Task<Appointment?> FindReadable(Guid id, string callerId, IReadOnlyCollection<string> roles)
    {
        var patient = roles.Contains(UserRoles.PATIENT); var practitionerRole = roles.Contains(UserRoles.PRACTITIONER); var organizationMember = roles.Contains(UserRoles.ORGANIZATION_MEMBER);
        return context.Appointments.FirstOrDefaultAsync(row => row.Id == id &&
            ((patient && context.Patients.Any(profile => profile.Id == row.PatientId && context.Users.Any(user => user.Id == callerId && user.PersonId == profile.PersonId)))
            || (practitionerRole && context.PractitionerRolesFor(callerId).Any(profile => profile.Id == row.PractitionerRoleId))
            || (organizationMember && context.OrganizationMemberships.Any(profile => profile.OrganizationId == row.OrganizationId && profile.UserId == callerId))));
    }
    public async Task<bool> TryBook(Appointment appointment, SessionSlot slot)
    {
        await context.Appointments.AddAsync(appointment); slot.IsAvailable = false;
        try { await context.SaveChangesAsync(); return true; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ApplicationDbContext.NonCancelledSlotIndexName })
        {
            context.Entry(appointment).State = EntityState.Detached;
            await context.Entry(slot).ReloadAsync(); return false;
        }
    }
    public Task<CancellationRequest?> FindCancellationRequest(Guid id) => context.CancellationRequests.FirstOrDefaultAsync(row => row.AppointmentId == id);
    public async Task AddCancellationRequest(CancellationRequest row) => await context.CancellationRequests.AddAsync(row);
    public void RemoveCancellationRequest(CancellationRequest row) => context.CancellationRequests.Remove(row);
    public async Task SaveChanges() => await context.SaveChangesAsync();
    public Task<bool> PatientExists(Guid id) => context.Patients.AnyAsync(row => row.Id == id);
    public Task<bool> PractitionerRoleExists(Guid id) => context.PractitionerRoles.AnyAsync(row => row.Id == id);
    public Task<bool> OrganizationExists(Guid id) => context.Organizations.AnyAsync(row => row.Id == id);
    public async Task<List<Appointment>> GetOwn(AppointmentFilter filter, string userId)
    {
        var patientId = await context.PatientsFor(userId).Select(row => row.Id).FirstOrDefaultAsync();
        var practitionerRoleId = await context.PractitionerRolesFor(userId).Select(row => row.Id).FirstOrDefaultAsync();
        return await Filter(context.Appointments.Where(row => row.PatientId == patientId || row.PractitionerRoleId == practitionerRoleId), filter).ToListAsync();
    }
    public async Task<List<Appointment>> GetOrganizationAppointments(AppointmentFilter filter, string userId)
    {
        var organizationId = await context.OrganizationMemberships.Where(row => row.UserId == userId).Select(row => row.OrganizationId).FirstOrDefaultAsync();
        return await Filter(context.Appointments.Where(row => row.OrganizationId == organizationId), filter).ToListAsync();
    }
    private static IQueryable<Appointment> Filter(IQueryable<Appointment> query, AppointmentFilter filter)
    {
        if (filter.FromDate.HasValue) query = query.Where(row => row.AppointmentDateTime.Date >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(row => row.AppointmentDateTime.Date <= filter.ToDate.Value);
        if (filter.PatientId.HasValue) query = query.Where(row => row.PatientId == filter.PatientId);
        if (filter.PractitionerRoleId.HasValue) query = query.Where(row => row.PractitionerRoleId == filter.PractitionerRoleId);
        if (filter.OrganizationId.HasValue) query = query.Where(row => row.OrganizationId == filter.OrganizationId);
        if (filter.Status.HasValue) query = query.Where(row => row.Status == filter.Status);
        return query;
    }
}
