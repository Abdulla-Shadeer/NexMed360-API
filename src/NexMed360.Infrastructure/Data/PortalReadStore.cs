using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Shared;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data;

// Queries only. Related roots are joined by ID and never reattached as domain navigations.
public sealed class PortalReadStore(ApplicationDbContext context) : IPortalReadStore
{
    public async Task<PortalReadData> Load(PortalReadRequest request)
    {
        var patients = request.PatientIds.Count + request.ProfileUserIds.Count == 0 ? []
            : await context.Patients.Where(row => request.PatientIds.Contains(row.Id) || request.ProfileUserIds.Contains(row.UserId)).ToDictionaryAsync(row => row.Id);
        var slots = request.SlotIds.Count == 0 ? new Dictionary<Guid, SessionSlot>()
            : await context.SessionSlots.Where(row => request.SlotIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var sessions = request.SessionIds.Count == 0 ? new Dictionary<Guid, DoctorSession>()
            : await context.DoctorSessions.Where(row => request.SessionIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var doctorIds = request.DoctorIds.Concat(sessions.Values.Select(row => row.DoctorId)).Distinct().ToArray();
        var doctors = doctorIds.Length + request.ProfileUserIds.Count == 0 ? []
            : await context.Doctors.Where(row => doctorIds.Contains(row.Id) || request.ProfileUserIds.Contains(row.UserId)).ToDictionaryAsync(row => row.Id);
        var admins = request.IncludeHospitalAdmins || request.ProfileUserIds.Count > 0
            ? await context.HospitalAdmins.Where(row => (request.IncludeHospitalAdmins && request.HospitalIds.Contains(row.HospitalId)) || request.ProfileUserIds.Contains(row.UserId)).ToListAsync() : [];
        var receptionists = request.ProfileUserIds.Count == 0 ? new List<Receptionist>()
            : await context.Receptionists.Where(row => request.ProfileUserIds.Contains(row.UserId)).ToListAsync();
        var hospitalIds = request.HospitalIds.Concat(doctors.Values.Select(row => row.HospitalId))
            .Concat(admins.Select(row => row.HospitalId)).Concat(receptionists.Select(row => row.HospitalId)).Distinct().ToArray();
        var hospitals = hospitalIds.Length == 0 ? new Dictionary<Guid, Hospital>()
            : await context.Hospitals.Where(row => hospitalIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var accountIds = request.AccountIds.Concat(patients.Values.Select(row => row.UserId)).Concat(doctors.Values.Select(row => row.UserId))
            .Concat(admins.Select(row => row.UserId)).Concat(receptionists.Select(row => row.UserId)).Distinct().ToArray();
        var accounts = accountIds.Length == 0 ? new Dictionary<string, AccountSummary>()
            : await context.Users.Where(row => accountIds.Contains(row.Id))
                .Select(row => new AccountSummary(row.Id, row.FirstName, row.LastName, row.Email, row.PhoneNumber)).ToDictionaryAsync(row => row.Id);
        if (request.IncludeAccountRoles && accounts.Count > 0)
        {
            var roles = await context.UserRoles.Where(row => accountIds.Contains(row.UserId))
                .Join(context.Roles, row => row.RoleId, role => role.Id, (row, role) => new { row.UserId, role.Name }).ToListAsync();
            foreach (var role in roles)
                if (role.Name != null) accounts[role.UserId].Roles.Add(role.Name);
        }
        return new PortalReadData { Patients = patients, Doctors = doctors, Hospitals = hospitals, Sessions = sessions,
            Slots = slots, Accounts = accounts, Admins = admins, Receptionists = receptionists };
    }
}
