using Microsoft.EntityFrameworkCore;
using NexMed360.Infrastructure.Data;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360Api.Services;

public class PatientRepository(ApplicationDbContext context) : IPatientRepository
{
    public async Task<Patient> Add(Patient patient)
    {
        var existing = await context.Patients.FirstOrDefaultAsync(row => row.PersonId == patient.PersonId);
        if (existing != null) return existing;
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient;
    }
    public Task<PractitionerRole?> FindPractitionerRole(Guid roleId, string userId) => context.PractitionerRolesFor(userId).FirstOrDefaultAsync(row => row.Id == roleId && row.IsActive);
    public Task<bool> PatientExists(Guid id) => context.Patients.AnyAsync(row => row.Id == id);
    public Task<Patient?> Find(Guid id) => context.Patients.FirstOrDefaultAsync(row => row.Id == id);
    public Task<bool> CanReadPatient(Guid id, string callerId, bool isPractitionerRole) => context.Patients.AnyAsync(row => row.Id == id
        && (isPractitionerRole || context.Users.Any(user => user.Id == callerId && user.PersonId == row.PersonId)));
}
