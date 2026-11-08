using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.Patient;
using Microsoft.EntityFrameworkCore;
using NexMed360.Infrastructure.Data;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360.Infrastructure.Repositories;

public class PatientRepository(ApplicationDbContext context) : IPatientRepository
{
    public async Task<Patient> Add(Patient patient)
    {
        var existing = await context.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.PersonId == patient.PersonId);
        if (existing != null) patient = existing;
        else context.Patients.Add(patient);
        if (context.Access.Mode == WorkspaceModes.Work && !await context.OrganizationPatients.AnyAsync(row => row.PatientId == patient.Id))
            context.OrganizationPatients.Add(new OrganizationPatient { OrganizationId = context.Access.OrganizationId!.Value, PatientId = patient.Id });
        await context.SaveChangesAsync();
        return patient;
    }
    public Task<PractitionerRole?> FindPractitionerRole(Guid roleId, string userId) => context.PractitionerRoles.Where(role => context.Practitioners.Any(practitioner => practitioner.Id == role.PractitionerId && context.Users.Any(user => user.Id == userId && user.PersonId == practitioner.PersonId))).FirstOrDefaultAsync(row => row.Id == roleId && row.IsActive
        && (!row.StartDate.HasValue || row.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow))
        && (!row.EndDate.HasValue || row.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow)));
    public Task<bool> PatientExists(Guid id) => context.Patients.AnyAsync(row => row.Id == id);
    public Task<Patient?> Find(Guid id) => context.Patients.FirstOrDefaultAsync(row => row.Id == id);
    public Task<bool> CanReadPatient(Guid id, string callerId, bool isPractitionerRole) => context.Patients.AnyAsync(row => row.Id == id
        && (isPractitionerRole || context.Users.Any(user => user.Id == callerId && user.PersonId == row.PersonId)));

    public async Task<ViewPatientDto?> View(Patient? row)
    {
        if (row == null) return null;
        return await (from person in context.People where person.Id == row.PersonId
            select new ViewPatientDto
            {
                Id = row.Id.ToString(), PersonId = person.Id.ToString(),
                UserId = context.Users.Where(user => user.PersonId == person.Id).Select(user => user.Id).FirstOrDefault() ?? "",
                FirstName = person.FirstName, LastName = person.LastName, Email = person.Email!, PhoneNumber = person.PhoneNumber!,
                BirthDate = row.BirthDate, Gender = row.Gender
            }).FirstOrDefaultAsync();
    }

}
