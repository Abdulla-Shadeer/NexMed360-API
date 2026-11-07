using NexMed360Api.Dtos.Search;
using NexMed360Api.Entities;

namespace NexMed360.Application.Search;
public interface ISearchData
{
    Task<List<string>> PatientUserIds(InputTypes type, string input);
    Task<List<string>> PractitionerRoleUserIds(InputTypes type, string input);
    Task<List<Organization>> Organizations(string input);
    Task<List<PractitionerRole>> PractitionerRoles(string? name, string? email, string? specialty);
}
