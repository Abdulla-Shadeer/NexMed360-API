using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Practitioners;
using NexMed360Api.Data;

namespace NexMed360.Infrastructure.Repositories;

public sealed class PractitionerRepository(ApplicationDbContext context) : IPractitionerRepository
{
    public Task<PractitionerProfileDto?> FindProfile(Guid id) =>
        (from practitioner in context.Practitioners.AsNoTracking()
         join person in context.People on practitioner.PersonId equals person.Id
         where practitioner.Id == id
         select new PractitionerProfileDto(practitioner.Id, person.FirstName, person.LastName, person.Email,
             person.PhoneNumber, practitioner.RegistrationNumber, practitioner.Qualifications)).FirstOrDefaultAsync();
}
