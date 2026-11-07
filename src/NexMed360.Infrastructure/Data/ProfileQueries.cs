using Microsoft.EntityFrameworkCore;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Data;

internal static class ProfileQueries
{
    internal static IQueryable<Patient> PatientsFor(this ApplicationDbContext context, string userId) =>
        context.Patients.Where(patient => context.Users.Any(user => user.Id == userId && user.PersonId == patient.PersonId));

    internal static IQueryable<PractitionerRole> PractitionerRolesFor(this ApplicationDbContext context, string userId) =>
        context.PractitionerRoles.Where(role => context.Practitioners.Any(practitioner => practitioner.Id == role.PractitionerId
            && context.Users.Any(user => user.Id == userId && user.PersonId == practitioner.PersonId)));
}
