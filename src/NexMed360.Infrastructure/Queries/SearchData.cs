using Microsoft.EntityFrameworkCore;
using NexMed360.Application.Search;
using NexMed360Api.Data;
using NexMed360Api.Entities;
using NexMed360Api.Dtos.Search;

namespace NexMed360Api.Services;
public class SearchData(ApplicationDbContext context) : ISearchData
{
    public Task<List<string>> PatientUserIds(InputTypes type, string input) => context.Patients
        .Join(context.Users, row => row.UserId, user => user.Id, (row, user) => user)
        .Where(user => type == InputTypes.Email ? EF.Functions.ILike(user.Email!, $"%{input}%") : EF.Functions.ILike(string.Concat(user.FirstName, " ", user.LastName), $"%{input}%"))
        .Select(user => user.Id).ToListAsync();
    public Task<List<string>> DoctorUserIds(InputTypes type, string input) => context.Doctors
        .Join(context.Users, row => row.UserId, user => user.Id, (row, user) => user)
        .Where(user => type == InputTypes.Email ? EF.Functions.ILike(user.Email!, $"%{input}%") : EF.Functions.ILike(string.Concat(user.FirstName, " ", user.LastName), $"%{input}%"))
        .Select(user => user.Id).ToListAsync();
    public Task<List<Hospital>> Hospitals(string input) => context.Hospitals.Where(row => EF.Functions.ILike(row.Name, $"%{input}%")).ToListAsync();
    public Task<List<Doctor>> Doctors(string? name, string? email, string? speciality) => context.Doctors
        .Join(context.Users, row => row.UserId, user => user.Id, (row, user) => new { Doctor = row, User = user })
        .Where(row => (String.IsNullOrEmpty(name) || EF.Functions.ILike(string.Concat(row.User.FirstName, " ", row.User.LastName), $"%{name}%"))
            && (String.IsNullOrEmpty(email) || EF.Functions.ILike(row.User.Email!, $"%{email}%"))
            && (String.IsNullOrEmpty(speciality) || EF.Functions.ILike(row.Doctor.Speciality, $"%{speciality}%")))
        .Select(row => row.Doctor).ToListAsync();
}
