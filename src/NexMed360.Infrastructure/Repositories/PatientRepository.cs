using Npgsql;
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
    public Task<Guid?> FindPersonIdForUser(string userId) => context.Users
        .Where(user => user.Id == userId && user.IsActive).Select(user => (Guid?)user.PersonId).FirstOrDefaultAsync();

    public Task<Patient?> FindForPerson(Guid personId) => context.Patients.IgnoreQueryFilters()
        .FirstOrDefaultAsync(patient => patient.PersonId == personId);

    public async Task<Patient> AddSelf(Patient patient)
    {
        // Self-enablement never adds an organization registration.
        context.Patients.Add(patient);
        try
        {
            await context.SaveChangesAsync();
            return patient;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Patients_PersonId" })
        {
            context.Entry(patient).State = EntityState.Detached;
            return (await FindForPerson(patient.PersonId))!;
        }
    }

    public Task<PractitionerRole?> FindPractitionerRole(Guid roleId, string userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var roles =
            from role in context.PractitionerRoles
            join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join user in context.Users on practitioner.PersonId equals user.PersonId
            where role.Id == roleId && user.Id == userId && role.IsActive
                && (!role.StartDate.HasValue || role.StartDate <= today)
                && (!role.EndDate.HasValue || role.EndDate >= today)
            select role;

        return roles.FirstOrDefaultAsync();
    }

    public Task<bool> PatientExists(Guid id) => context.Patients.AnyAsync(row => row.Id == id);
    public Task<Patient?> Find(Guid id) => context.Patients.FirstOrDefaultAsync(row => row.Id == id);
    public Task<bool> CanReadPatient(Guid id, string callerId, bool isPractitionerRole) => context.Patients.AnyAsync(row => row.Id == id
        && (isPractitionerRole || context.Users.Any(user => user.Id == callerId && user.PersonId == row.PersonId)));

    public async Task<ViewPatientDto?> View(Patient? row)
    {
        if (row == null) return null;
        var patient =
            from person in context.People
            where person.Id == row.PersonId
            select new ViewPatientDto
            {
                Id = row.Id.ToString(), PersonId = person.Id.ToString(),
                UserId = context.Users.Where(user => user.PersonId == person.Id).Select(user => user.Id).FirstOrDefault() ?? "",
                FirstName = person.FirstName, LastName = person.LastName, Email = person.Email!, PhoneNumber = person.PhoneNumber!,
                BirthDate = row.BirthDate, Gender = row.Gender
            };

        return await patient.FirstOrDefaultAsync();
    }

}
