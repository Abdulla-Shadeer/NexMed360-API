using NexMed360Api.Dtos.PractitionerRole;
using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360.Infrastructure.Repositories;

public class PractitionerRoleRepository(ApplicationDbContext context) : IPractitionerRoleRepository
{
    public async Task LockAccount(string userId)
    {
        if (!context.Database.IsNpgsql()) return;
        if (context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Practitioner creation needs a registration transaction.");

        // Hold the account row until commit so repeated requests check in sequence.
        await context.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "AspNetUsers" WHERE "Id" = {userId} FOR UPDATE""");
    }

    public Task<List<PractitionerRole>> GetAssignments(Guid practitionerId, Guid organizationId) =>
        context.PractitionerRoles.IgnoreQueryFilters().Where(role => role.PractitionerId == practitionerId && role.OrganizationId == organizationId).ToListAsync();

    public async Task<PractitionerRole?> Add(PractitionerRole row)
    {
        context.PractitionerRoles.Add(row);
        await context.SaveChangesAsync();
        return row;
    }

    public async Task<Practitioner> FindOrAddPractitioner(Guid personId)
    {
        var practitioner = await context.Practitioners.FirstOrDefaultAsync(row => row.PersonId == personId);
        if (practitioner != null) return practitioner;
        practitioner = new Practitioner { PersonId = personId };
        context.Practitioners.Add(practitioner);
        await context.SaveChangesAsync();
        return practitioner;
    }

    public Task<List<ViewPractitionerRoleDto>> Views(IEnumerable<PractitionerRole> source)
    {
        var ids = source.Select(row => row.Id).ToArray();

        var roles =
            from role in context.PractitionerRoles
            join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join person in context.People on practitioner.PersonId equals person.Id
            join organization in context.Organizations on role.OrganizationId equals organization.Id
            where ids.Contains(role.Id)
            select new ViewPractitionerRoleDto
            {
                Id = role.Id.ToString(), PractitionerId = role.PractitionerId.ToString(), RoleName = role.RoleName, Specialty = role.Specialty,
                UserId = context.Users.Where(user => user.PersonId == person.Id).Select(user => user.Id).FirstOrDefault() ?? "",
                FirstName = person.FirstName, LastName = person.LastName,
                Email = person.Email!, PhoneNumber = person.PhoneNumber!,
                OrganizationId = organization.Id.ToString(), OrganizationName = organization.Name
            };

        return roles.ToListAsync();
    }

}
