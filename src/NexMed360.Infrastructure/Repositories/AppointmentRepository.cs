using NexMed360Api.Dtos.Appointment;
using NexMed360.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NexMed360Api.Common.Auth;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure.Repositories;

public class AppointmentRepository(ApplicationDbContext context) : IAppointmentRepository
{
    public async Task<SessionSlot?> FindSlot(Guid id)
    {
        var slot = await context.SessionSlots.FirstOrDefaultAsync(row => row.Id == id);
        if (slot != null) slot.Session = (await context.PractitionerSchedules.FirstOrDefaultAsync(row => row.Id == slot.SessionId))!;
        return slot;
    }
    public Task<Appointment?> Find(Guid id) => context.Appointments.FirstOrDefaultAsync(row => row.Id == id);
    public Task<Appointment?> FindReadable(Guid id, string callerId, IReadOnlyCollection<string> permissions)
    {
        if (context.Access.IsSelected)
            return context.Appointments.FirstOrDefaultAsync(row => row.Id == id);
        // Calls outside HTTP still need a matching patient, assignment, or membership.
        var patient = permissions.Contains(WorkspaceModes.Patient); var practitionerRole = permissions.Contains(Permissions.EncountersStart); var organizationMember = permissions.Contains(Permissions.AppointmentsManage);
        return context.Appointments.FirstOrDefaultAsync(row => row.Id == id &&
            ((patient && context.Patients.Any(profile => profile.Id == row.PatientId && context.Users.Any(user => user.Id == callerId && user.PersonId == profile.PersonId)))
            || (practitionerRole && context.PractitionerRoles.Where(role => context.Practitioners.Any(practitioner => practitioner.Id == role.PractitionerId && context.Users.Any(user => user.Id == callerId && user.PersonId == practitioner.PersonId))).Any(profile => profile.Id == row.PractitionerRoleId))
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
        var patientId = await context.Patients.Where(patient => context.Users.Any(user => user.Id == userId && user.PersonId == patient.PersonId)).Select(row => row.Id).FirstOrDefaultAsync();
        if (context.Access.Mode == WorkspaceModes.Work)
            return await Filter(context.Appointments.Where(row => row.PractitionerRoleId == context.Access.PractitionerRoleId), filter).ToListAsync();
        var practitionerRoleId = await context.PractitionerRoles.Where(role => context.Practitioners.Any(practitioner => practitioner.Id == role.PractitionerId && context.Users.Any(user => user.Id == userId && user.PersonId == practitioner.PersonId))).Select(row => row.Id).FirstOrDefaultAsync();
        return await Filter(context.Appointments.Where(row => row.PatientId == patientId || row.PractitionerRoleId == practitionerRoleId), filter).ToListAsync();
    }
    public async Task<List<Appointment>> GetOrganizationAppointments(AppointmentFilter filter, string userId)
    {
        if (context.Access.Mode == WorkspaceModes.Work) return await Filter(context.Appointments, filter).ToListAsync();
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

    public async Task<List<ViewAppointmentDto>> Views(IEnumerable<Appointment> source)
    {
        var rows = source.ToList();
        var patientIds = rows.Select(row => row.PatientId).Distinct().ToArray();
        var patients = await (from patient in context.Patients join person in context.People on patient.PersonId equals person.Id
            where patientIds.Contains(patient.Id) select new { patient.Id, Person = person }).ToDictionaryAsync(row => row.Id, row => row.Person);
        var roleIds = rows.Select(row => row.PractitionerRoleId).Distinct().ToArray();
        var practitioners = await (from role in context.PractitionerRoles join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join person in context.People on practitioner.PersonId equals person.Id
            where roleIds.Contains(role.Id) select new { role.Id, role.Specialty, Person = person }).ToDictionaryAsync(row => row.Id);
        var organizationIds = rows.Select(row => row.OrganizationId).Distinct().ToArray();
        var organizations = await context.Organizations.Where(row => organizationIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var accountIds = rows.Select(row => row.CreatedByUserId).Distinct().ToArray();
        var accounts = await context.Users.Where(row => accountIds.Contains(row.Id)).Select(row => new { row.Id, Name = row.Person.FirstName + " " + row.Person.LastName }).ToDictionaryAsync(row => row.Id, row => row.Name);
        var slotIds = rows.Select(row => row.SessionSlotId).Distinct().ToArray();
        var slots = await context.SessionSlots.Where(row => slotIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, row => row.SlotNumber);
        return rows.Select(row =>
        {
            var patient = patients.GetValueOrDefault(row.PatientId);
            var practitioner = practitioners.GetValueOrDefault(row.PractitionerRoleId);
            var organization = organizations.GetValueOrDefault(row.OrganizationId);
            return new ViewAppointmentDto
            {
                BookingReference = row.Id.ToString(), PatientId = row.PatientId.ToString(),
                PatientName = patient == null ? null! : patient.FirstName + " " + patient.LastName,
                PatientEmail = patient?.Email!, PatientPhone = patient?.PhoneNumber!,
                PractitionerRoleId = row.PractitionerRoleId.ToString(),
                PractitionerName = practitioner == null ? null! : practitioner.Person.FirstName + " " + practitioner.Person.LastName,
                PractitionerSpecialty = practitioner?.Specialty!, OrganizationId = row.OrganizationId.ToString(),
                OrganizationName = organization?.Name!, OrganizationPhone = organization?.Phone!,
                OrganizationAddress = organization == null ? null! : $"{organization.Street}, {organization.City}, {organization.District}",
                CreatedByUserId = row.CreatedByUserId, CreatedByUserName = accounts.GetValueOrDefault(row.CreatedByUserId)!,
                AppointmentDateTime = row.AppointmentDateTime.ToString(), AppointmentNumber = slots.GetValueOrDefault(row.SessionSlotId),
                Remarks = row.Remarks, Status = row.Status
            };
        }).ToList();
    }
    public async Task<ViewAppointmentDto?> View(Appointment? row) => row == null ? null : (await Views([row]))[0];

}
