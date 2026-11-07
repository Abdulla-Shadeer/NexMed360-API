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
        if (slot != null) slot.Session = (await context.DoctorSessions.FirstOrDefaultAsync(row => row.Id == slot.SessionId))!;
        return slot;
    }
    public Task<Appointment?> Find(Guid id) => context.Appointments.FirstOrDefaultAsync(row => row.Id == id);
    public Task<Appointment?> FindReadable(Guid id, string callerId, IReadOnlyCollection<string> roles)
    {
        var patient = roles.Contains(UserRoles.PATIENT); var doctor = roles.Contains(UserRoles.DOCTOR); var receptionist = roles.Contains(UserRoles.RECEPTIONIST);
        return context.Appointments.FirstOrDefaultAsync(row => row.Id == id &&
            ((patient && context.Patients.Any(profile => profile.Id == row.PatientId && profile.UserId == callerId))
            || (doctor && context.Doctors.Any(profile => profile.Id == row.DoctorId && profile.UserId == callerId))
            || (receptionist && context.Receptionists.Any(profile => profile.HospitalId == row.HospitalId && profile.UserId == callerId))));
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
    public Task<bool> DoctorExists(Guid id) => context.Doctors.AnyAsync(row => row.Id == id);
    public Task<bool> HospitalExists(Guid id) => context.Hospitals.AnyAsync(row => row.Id == id);
    public async Task<List<Appointment>> GetOwn(AppointmentFilter filter, string userId)
    {
        var patientId = await context.Patients.Where(row => row.UserId == userId).Select(row => row.Id).FirstOrDefaultAsync();
        var doctorId = await context.Doctors.Where(row => row.UserId == userId).Select(row => row.Id).FirstOrDefaultAsync();
        return await Filter(context.Appointments.Where(row => row.PatientId == patientId || row.DoctorId == doctorId), filter).ToListAsync();
    }
    public async Task<List<Appointment>> GetHospitalAppointments(AppointmentFilter filter, string userId)
    {
        var hospitalId = await context.Receptionists.Where(row => row.UserId == userId).Select(row => row.HospitalId).FirstOrDefaultAsync();
        return await Filter(context.Appointments.Where(row => row.HospitalId == hospitalId), filter).ToListAsync();
    }
    private static IQueryable<Appointment> Filter(IQueryable<Appointment> query, AppointmentFilter filter)
    {
        if (filter.FromDate.HasValue) query = query.Where(row => row.AppointmentDateTime.Date >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(row => row.AppointmentDateTime.Date <= filter.ToDate.Value);
        if (filter.PatientId.HasValue) query = query.Where(row => row.PatientId == filter.PatientId);
        if (filter.DoctorId.HasValue) query = query.Where(row => row.DoctorId == filter.DoctorId);
        if (filter.HospitalId.HasValue) query = query.Where(row => row.HospitalId == filter.HospitalId);
        if (filter.Status.HasValue) query = query.Where(row => row.Status == filter.Status);
        return query;
    }
}
