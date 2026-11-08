using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Search;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Organization;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure.Repositories;

public class SearchRepository(ApplicationDbContext context, IPractitionerRoleRepository practitioners, IOrganizationRepository organizations) : ISearchRepository
{
    public Task<List<ViewPatientDto>> Patients(InputTypes type, string input) =>
        (from patient in context.Patients join person in context.People on patient.PersonId equals person.Id
         where type == InputTypes.Email ? EF.Functions.ILike(person.Email!, $"%{input}%") : EF.Functions.ILike(person.FirstName + " " + person.LastName, $"%{input}%")
         select new ViewPatientDto
         {
             Id = patient.Id.ToString(), PersonId = person.Id.ToString(),
             UserId = context.Users.Where(user => user.PersonId == person.Id).Select(user => user.Id).FirstOrDefault() ?? "",
             FirstName = person.FirstName, LastName = person.LastName, Email = person.Email!, PhoneNumber = person.PhoneNumber!,
             Gender = patient.Gender, BirthDate = patient.BirthDate
         }).ToListAsync();
    public async Task<List<ViewOrganizationDto>> Organizations(string input) => await organizations.Views(
        await context.Organizations.Where(row => EF.Functions.ILike(row.Name, $"%{input}%")).ToListAsync());
    public async Task<List<ViewPractitionerRoleDto>> PractitionerRoles(string? name, string? email, string? specialty) => await practitioners.Views(
        await (from role in context.PractitionerRoles join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
               join person in context.People on practitioner.PersonId equals person.Id
               where (string.IsNullOrEmpty(name) || EF.Functions.ILike(person.FirstName + " " + person.LastName, $"%{name}%"))
                   && (string.IsNullOrEmpty(email) || EF.Functions.ILike(person.Email!, $"%{email}%"))
                   && (string.IsNullOrEmpty(specialty) || EF.Functions.ILike(role.Specialty, $"%{specialty}%"))
               select role).ToListAsync());
}
