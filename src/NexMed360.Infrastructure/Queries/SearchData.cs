using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Search;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Dtos.Search;

namespace NexMed360Api.Services;
public class SearchData(ApplicationDbContext context) : ISearchData
{
    public Task<List<string>> PatientUserIds(InputTypes type, string input) => context.Patients
        .Join(context.Users, row => row.PersonId, user => user.PersonId, (row, user) => user)
        .Where(user => type == InputTypes.Email ? EF.Functions.ILike(user.Email!, $"%{input}%") : EF.Functions.ILike(string.Concat(user.Person.FirstName, " ", user.Person.LastName), $"%{input}%"))
        .Select(user => user.Id).ToListAsync();
    public Task<List<string>> PractitionerRoleUserIds(InputTypes type, string input) => context.PractitionerRoles.Join(context.Practitioners, role => role.PractitionerId, practitioner => practitioner.Id, (role, practitioner) => practitioner)
        .Join(context.Users, row => row.PersonId, user => user.PersonId, (row, user) => user)
        .Where(user => type == InputTypes.Email ? EF.Functions.ILike(user.Email!, $"%{input}%") : EF.Functions.ILike(string.Concat(user.Person.FirstName, " ", user.Person.LastName), $"%{input}%"))
        .Select(user => user.Id).ToListAsync();
    public Task<List<Organization>> Organizations(string input) => context.Organizations.Where(row => EF.Functions.ILike(row.Name, $"%{input}%")).ToListAsync();
    public Task<List<PractitionerRole>> PractitionerRoles(string? name, string? email, string? specialty) => context.PractitionerRoles.Join(context.Practitioners, role => role.PractitionerId, practitioner => practitioner.Id, (role, practitioner) => new { Role = role, practitioner.PersonId })
        .Join(context.Users, row => row.PersonId, user => user.PersonId, (row, user) => new { PractitionerRole = row.Role, ApplicationUser = user })
        .Where(row => (String.IsNullOrEmpty(name) || EF.Functions.ILike(string.Concat(row.ApplicationUser.Person.FirstName, " ", row.ApplicationUser.Person.LastName), $"%{name}%"))
            && (String.IsNullOrEmpty(email) || EF.Functions.ILike(row.ApplicationUser.Email!, $"%{email}%"))
            && (String.IsNullOrEmpty(specialty) || EF.Functions.ILike(row.PractitionerRole.Specialty, $"%{specialty}%")))
        .Select(row => row.PractitionerRole).ToListAsync();
}
