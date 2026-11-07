using NexMed360Api.Dtos.Search;
using NexMed360Api.Entities;

namespace NexMed360.Application.Search;
public interface ISearchData
{
    Task<List<string>> PatientUserIds(InputTypes type, string input);
    Task<List<string>> DoctorUserIds(InputTypes type, string input);
    Task<List<Hospital>> Hospitals(string input);
    Task<List<Doctor>> Doctors(string? name, string? email, string? speciality);
}
