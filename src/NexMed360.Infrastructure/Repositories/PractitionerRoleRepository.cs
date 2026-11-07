using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;
namespace NexMed360Api.Services;

public class PractitionerRoleRepository(ApplicationDbContext context) : IPractitionerRoleRepository
{
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
}
