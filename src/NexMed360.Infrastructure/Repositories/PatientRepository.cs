using NexMed360.Application.Patients;
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
    public Task<List<PatientIdentityDto>> SearchIdentities(PatientIdentityFilter filter)
    {
        var name = filter.Name?.ToLowerInvariant();
        var email = filter.Email?.ToLowerInvariant();
        return (from patient in context.Patients.IgnoreQueryFilters().AsNoTracking()
            join person in context.People on patient.PersonId equals person.Id
            where (!filter.PatientId.HasValue || patient.Id == filter.PatientId)
                && (string.IsNullOrEmpty(name) || (person.FirstName + " " + person.LastName).ToLower().Contains(name))
                && (string.IsNullOrEmpty(email) || person.Email != null && person.Email.ToLower() == email)
            orderby patient.Id
            select new PatientIdentityDto(patient.Id, person.FirstName, person.LastName, patient.BirthDate, patient.Gender, person.Email))
            .Skip(filter.Offset).Take(filter.Count).ToListAsync();
    }

    public async Task<PatientIdentityDto?> FindIdentity(Guid id) =>
        (await SearchIdentities(new(PatientId: id, Count: 1))).FirstOrDefault();

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
                Id = row.Id.ToString(), PersonId = context.Access.Mode == WorkspaceModes.Work ? "" : person.Id.ToString(),
                UserId = context.Access.Mode == WorkspaceModes.Work ? "" : context.Users.Where(user => user.PersonId == person.Id).Select(user => user.Id).FirstOrDefault() ?? "",
                FirstName = person.FirstName, LastName = person.LastName, Email = person.Email!, PhoneNumber = person.PhoneNumber!,
                BirthDate = row.BirthDate, Gender = row.Gender
            };

        return await patient.FirstOrDefaultAsync();
    }

}
